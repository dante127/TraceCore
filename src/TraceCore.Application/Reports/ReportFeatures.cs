using MediatR;
using Microsoft.EntityFrameworkCore;
using TraceCore.Application.Common.Interfaces;
using TraceCore.Domain.Enums;
using TaskStatus = TraceCore.Domain.Enums.TaskStatus;

namespace TraceCore.Application.Reports;

public sealed record SlaComplianceReportDto(
    int TotalCasesEvaluated,
    int SlaCompliantCases,
    int SlaBreachedCases,
    double ComplianceRatePercentage,
    Dictionary<string, int> BreachesByPriority);

public sealed record RiskDistributionReportDto(
    int TotalCases,
    int LowRiskCount,
    int MediumRiskCount,
    int HighRiskCount,
    int CriticalRiskCount,
    double AverageRiskScore);

public sealed record InvestigatorWorkloadItemDto(
    Guid InvestigatorId,
    int ActiveCaseCount,
    int PendingTasksCount,
    int OverdueTasksCount);

public sealed record InvestigatorWorkloadReportDto(
    IReadOnlyList<InvestigatorWorkloadItemDto> Investigators);

// 1. SLA Compliance Report
public sealed record GetSlaComplianceReportQuery : IRequest<SlaComplianceReportDto>;

public class GetSlaComplianceReportQueryHandler : IRequestHandler<GetSlaComplianceReportQuery, SlaComplianceReportDto>
{
    private readonly IApplicationDbContext _context;

    public GetSlaComplianceReportQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<SlaComplianceReportDto> Handle(GetSlaComplianceReportQuery request, CancellationToken cancellationToken)
    {
        int total = await _context.Cases.CountAsync(cancellationToken);
        int breached = await _context.Cases.CountAsync(c => c.IsSlaBreached, cancellationToken);
        int compliant = total - breached;
        double rate = total > 0 ? Math.Round((compliant / (double)total) * 100, 2) : 100.0;

        var breachesByPriority = await _context.Cases
            .AsNoTracking()
            .Where(c => c.IsSlaBreached)
            .GroupBy(c => c.Priority)
            .Select(g => new { Key = g.Key.ToString(), Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

        return new SlaComplianceReportDto(total, compliant, breached, rate, breachesByPriority);
    }
}

// 2. Risk Distribution Report
public sealed record GetRiskDistributionReportQuery : IRequest<RiskDistributionReportDto>;

public class GetRiskDistributionReportQueryHandler : IRequestHandler<GetRiskDistributionReportQuery, RiskDistributionReportDto>
{
    private readonly IApplicationDbContext _context;

    public GetRiskDistributionReportQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<RiskDistributionReportDto> Handle(GetRiskDistributionReportQuery request, CancellationToken cancellationToken)
    {
        int total = await _context.Cases.CountAsync(cancellationToken);

        var byLevel = await _context.Cases
            .AsNoTracking()
            .GroupBy(c => c.CurrentRiskLevel)
            .Select(g => new { Level = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Level, x => x.Count, cancellationToken);

        double avg = total > 0
            ? Math.Round(await _context.Cases.AverageAsync(c => (double?)c.CurrentRiskScore, cancellationToken) ?? 0.0, 1)
            : 0.0;

        return new RiskDistributionReportDto(
            total,
            byLevel.GetValueOrDefault(RiskLevel.Low),
            byLevel.GetValueOrDefault(RiskLevel.Medium),
            byLevel.GetValueOrDefault(RiskLevel.High),
            byLevel.GetValueOrDefault(RiskLevel.Critical),
            avg);
    }
}

// 3. Investigator Workload Report
public sealed record GetInvestigatorWorkloadReportQuery : IRequest<InvestigatorWorkloadReportDto>;

public class GetInvestigatorWorkloadReportQueryHandler : IRequestHandler<GetInvestigatorWorkloadReportQuery, InvestigatorWorkloadReportDto>
{
    private readonly IApplicationDbContext _context;

    public GetInvestigatorWorkloadReportQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<InvestigatorWorkloadReportDto> Handle(GetInvestigatorWorkloadReportQuery request, CancellationToken cancellationToken)
    {
        var activeCases = await _context.Cases
            .AsNoTracking()
            .Where(c => c.AssignedInvestigatorId.HasValue && (c.Status == CaseStatus.Open || c.Status == CaseStatus.UnderInvestigation))
            .GroupBy(c => c.AssignedInvestigatorId!.Value)
            .Select(g => new { InvestigatorId = g.Key, CaseCount = g.Count() })
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var tasks = await _context.Tasks
            .AsNoTracking()
            .Where(t => t.AssignedToUserId.HasValue && t.Status != TaskStatus.Completed && t.Status != TaskStatus.Cancelled)
            .GroupBy(t => t.AssignedToUserId!.Value)
            .Select(g => new
            {
                InvestigatorId = g.Key,
                PendingCount = g.Count(),
                OverdueCount = g.Count(t => t.DueDateUtc.HasValue && t.DueDateUtc.Value < now)
            })
            .ToListAsync(cancellationToken);

        var allIds = activeCases.Select(c => c.InvestigatorId)
            .Union(tasks.Select(t => t.InvestigatorId))
            .Distinct();

        var caseCounts = activeCases.ToDictionary(c => c.InvestigatorId, c => c.CaseCount);
        var taskLookup = tasks.ToDictionary(t => t.InvestigatorId);

        var list = allIds.Select(id =>
        {
            caseCounts.TryGetValue(id, out int caseCount);
            taskLookup.TryGetValue(id, out var tInfo);
            return new InvestigatorWorkloadItemDto(
                id,
                caseCount,
                tInfo?.PendingCount ?? 0,
                tInfo?.OverdueCount ?? 0);
        }).ToList();

        return new InvestigatorWorkloadReportDto(list);
    }
}
