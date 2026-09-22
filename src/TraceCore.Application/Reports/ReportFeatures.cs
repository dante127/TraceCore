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
        var cases = await _context.Cases
            .AsNoTracking()
            .Select(c => new { c.Priority, c.IsSlaBreached })
            .ToListAsync(cancellationToken);

        int total = cases.Count;
        int breached = cases.Count(c => c.IsSlaBreached);
        int compliant = total - breached;
        double rate = total > 0 ? Math.Round((compliant / (double)total) * 100, 2) : 100.0;

        var breachesByPriority = cases
            .Where(c => c.IsSlaBreached)
            .GroupBy(c => c.Priority.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

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
        var cases = await _context.Cases
            .AsNoTracking()
            .Select(c => new { c.CurrentRiskScore, c.CurrentRiskLevel })
            .ToListAsync(cancellationToken);

        int total = cases.Count;
        int low = cases.Count(c => c.CurrentRiskLevel == RiskLevel.Low);
        int med = cases.Count(c => c.CurrentRiskLevel == RiskLevel.Medium);
        int high = cases.Count(c => c.CurrentRiskLevel == RiskLevel.High);
        int crit = cases.Count(c => c.CurrentRiskLevel == RiskLevel.Critical);
        double avg = total > 0 ? Math.Round(cases.Average(c => c.CurrentRiskScore), 1) : 0.0;

        return new RiskDistributionReportDto(total, low, med, high, crit, avg);
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

        var list = allIds.Select(id =>
        {
            int caseCount = activeCases.FirstOrDefault(c => c.InvestigatorId == id)?.CaseCount ?? 0;
            var tInfo = tasks.FirstOrDefault(t => t.InvestigatorId == id);
            return new InvestigatorWorkloadItemDto(
                id,
                caseCount,
                tInfo?.PendingCount ?? 0,
                tInfo?.OverdueCount ?? 0);
        }).ToList();

        return new InvestigatorWorkloadReportDto(list);
    }
}
