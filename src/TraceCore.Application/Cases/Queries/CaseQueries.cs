using MediatR;
using Microsoft.EntityFrameworkCore;
using TraceCore.Application.Common.Exceptions;
using TraceCore.Application.Common.Interfaces;
using TraceCore.Application.Common.Models;
using TraceCore.Domain.Entities.Cases;
using TraceCore.Domain.Enums;
using TaskStatus = TraceCore.Domain.Enums.TaskStatus;

namespace TraceCore.Application.Cases.Queries;

// 1. Get Case By Id
public sealed record GetCaseByIdQuery(Guid Id) : IRequest<CaseDetailDto>;

public class GetCaseByIdQueryHandler : IRequestHandler<GetCaseByIdQuery, CaseDetailDto>
{
    private readonly IApplicationDbContext _context;

    public GetCaseByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CaseDetailDto> Handle(GetCaseByIdQuery request, CancellationToken cancellationToken)
    {
        var @case = await _context.Cases
            .Include(c => c.Persons)
            .Include(c => c.Organizations)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Case), request.Id);

        // Fetch participant names
        var personIds = @case.Persons.Select(p => p.PersonId).ToList();
        var people = await _context.People
            .AsNoTracking()
            .Where(p => personIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => new { p.DisplayName, Email = p.Email ?? string.Empty }, cancellationToken);

        var orgIds = @case.Organizations.Select(o => o.OrganizationId).ToList();
        var orgs = await _context.Organizations
            .AsNoTracking()
            .Where(o => orgIds.Contains(o.Id))
            .ToDictionaryAsync(o => o.Id, o => o.Name, cancellationToken);

        var participantDtos = @case.Persons.Select(p =>
        {
            people.TryGetValue(p.PersonId, out var val);
            return new CaseParticipantDto(
                p.Id,
                p.PersonId,
                val?.DisplayName ?? "Unknown",
                val?.Email ?? "",
                p.Role,
                p.Notes,
                p.AssignedAtUtc);
        }).ToList();

        var orgDtos = @case.Organizations.Select(o => new CaseOrganizationDto(
            o.Id,
            o.OrganizationId,
            orgs.TryGetValue(o.OrganizationId, out var name) ? name : "Unknown",
            o.Role,
            o.Notes,
            o.AssignedAtUtc)).ToList();

        var openTasks = await _context.Tasks
            .CountAsync(t => t.CaseId == request.Id && t.Status != TaskStatus.Completed && t.Status != TaskStatus.Cancelled, cancellationToken);

        var evidenceCount = await _context.Evidence
            .CountAsync(e => e.CaseId == request.Id, cancellationToken);

        var investigationCount = await _context.Investigations
            .CountAsync(i => i.CaseId == request.Id, cancellationToken);

        return new CaseDetailDto(
            @case.Id,
            @case.CaseNumber,
            @case.Title,
            @case.Description,
            @case.Type,
            @case.Status,
            @case.Priority,
            @case.ConfidentialityLevel,
            @case.CreatedByUserId,
            @case.AssignedTeamId,
            @case.AssignedInvestigatorId,
            @case.OpenedAtUtc,
            @case.DueDateUtc,
            @case.ClosedAtUtc,
            @case.SlaTargetHours,
            @case.SlaDeadlineUtc,
            @case.IsSlaBreached,
            @case.SlaBreachedAtUtc,
            @case.CurrentRiskScore,
            @case.CurrentRiskLevel,
            @case.CreatedAtUtc,
            @case.UpdatedAtUtc,
            Convert.ToBase64String(@case.RowVersion),
            participantDtos,
            orgDtos,
            openTasks,
            evidenceCount,
            investigationCount);
    }
}

// 2. Get Cases Paged
public sealed class GetCasesPagedQuery : PagedRequest, IRequest<PagedList<CaseDto>>
{
    public CaseStatus? Status { get; set; }
    public CasePriority? Priority { get; set; }
    public CaseType? Type { get; set; }
    public Guid? AssignedInvestigatorId { get; set; }
    public bool? IsSlaBreached { get; set; }
    public string? SearchTerm { get; set; }
}

public class GetCasesPagedQueryHandler : IRequestHandler<GetCasesPagedQuery, PagedList<CaseDto>>
{
    private readonly IApplicationDbContext _context;

    public GetCasesPagedQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PagedList<CaseDto>> Handle(GetCasesPagedQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Cases.AsNoTracking();

        if (request.Status.HasValue)
            query = query.Where(c => c.Status == request.Status.Value);
        if (request.Priority.HasValue)
            query = query.Where(c => c.Priority == request.Priority.Value);
        if (request.Type.HasValue)
            query = query.Where(c => c.Type == request.Type.Value);
        if (request.AssignedInvestigatorId.HasValue)
            query = query.Where(c => c.AssignedInvestigatorId == request.AssignedInvestigatorId.Value);
        if (request.IsSlaBreached.HasValue)
            query = query.Where(c => c.IsSlaBreached == request.IsSlaBreached.Value);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(c => c.Title.ToLower().Contains(term) ||
                                     c.CaseNumber.ToLower().Contains(term) ||
                                     c.Description.ToLower().Contains(term));
        }

        int totalCount = await query.CountAsync(cancellationToken);

        query = request.SortBy?.ToLower() switch
        {
            "casenumber" => request.SortDescending ? query.OrderByDescending(c => c.CaseNumber) : query.OrderBy(c => c.CaseNumber),
            "priority" => request.SortDescending ? query.OrderByDescending(c => c.Priority) : query.OrderBy(c => c.Priority),
            "status" => request.SortDescending ? query.OrderByDescending(c => c.Status) : query.OrderBy(c => c.Status),
            "risk" => request.SortDescending ? query.OrderByDescending(c => c.CurrentRiskScore) : query.OrderBy(c => c.CurrentRiskScore),
            "sladeadline" => request.SortDescending ? query.OrderByDescending(c => c.SlaDeadlineUtc) : query.OrderBy(c => c.SlaDeadlineUtc),
            _ => query.OrderByDescending(c => c.CreatedAtUtc)
        };

        var items = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(c => new CaseDto(
                c.Id,
                c.CaseNumber,
                c.Title,
                c.Type,
                c.Status,
                c.Priority,
                c.ConfidentialityLevel,
                c.CreatedByUserId,
                c.AssignedTeamId,
                c.AssignedInvestigatorId,
                c.OpenedAtUtc,
                c.DueDateUtc,
                c.ClosedAtUtc,
                c.SlaTargetHours,
                c.SlaDeadlineUtc,
                c.IsSlaBreached,
                c.CurrentRiskScore,
                c.CurrentRiskLevel,
                c.CreatedAtUtc,
                c.UpdatedAtUtc,
                Convert.ToBase64String(c.RowVersion)))
            .ToListAsync(cancellationToken);

        return new PagedList<CaseDto>(items, totalCount, request.PageNumber, request.PageSize);
    }
}

// 3. Get Case Timeline
public sealed record GetCaseTimelineQuery(Guid CaseId) : IRequest<IReadOnlyList<CaseTimelineItemDto>>;

public class GetCaseTimelineQueryHandler : IRequestHandler<GetCaseTimelineQuery, IReadOnlyList<CaseTimelineItemDto>>
{
    private readonly IApplicationDbContext _context;

    public GetCaseTimelineQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<CaseTimelineItemDto>> Handle(GetCaseTimelineQuery request, CancellationToken cancellationToken)
    {
        var timeline = new List<CaseTimelineItemDto>();

        // 1. Audit logs for Case
        var auditLogs = await _context.AuditLogs
            .AsNoTracking()
            .Where(a => a.EntityId == request.CaseId.ToString())
            .OrderByDescending(a => a.TimestampUtc)
            .Take(50)
            .ToListAsync(cancellationToken);

        foreach (var audit in auditLogs)
        {
            timeline.Add(new CaseTimelineItemDto(
                audit.Id.ToString(),
                "Audit",
                audit.Action,
                $"Case event: {audit.Action}",
                audit.TimestampUtc,
                audit.UserId?.ToString() ?? "System",
                audit.CorrelationId));
        }

        // 2. Custody events for Evidence under this Case (ID list bounded to avoid SQL parameter limits)
        var evidenceIds = await _context.Evidence
            .AsNoTracking()
            .Where(e => e.CaseId == request.CaseId)
            .Select(e => e.Id)
            .Take(500)
            .ToListAsync(cancellationToken);

        var custodyEvents = await _context.EvidenceCustodyEvents
            .AsNoTracking()
            .Where(c => evidenceIds.Contains(c.EvidenceId))
            .OrderByDescending(c => c.TimestampUtc)
            .Take(50)
            .ToListAsync(cancellationToken);

        foreach (var ce in custodyEvents)
        {
            timeline.Add(new CaseTimelineItemDto(
                ce.Id.ToString(),
                "EvidenceCustody",
                $"Custody {ce.Action}",
                $"{ce.Action} at {ce.Location}: {ce.Notes}",
                ce.TimestampUtc,
                ce.FromUserId.ToString(),
                ce.CurrentHash));
        }

        // 3. Investigation Activities (ID list bounded to avoid SQL parameter limits)
        var investigationIds = await _context.Investigations
            .AsNoTracking()
            .Where(i => i.CaseId == request.CaseId)
            .Select(i => i.Id)
            .Take(500)
            .ToListAsync(cancellationToken);

        var activities = await _context.InvestigationActivities
            .AsNoTracking()
            .Where(a => investigationIds.Contains(a.InvestigationId))
            .OrderByDescending(a => a.PerformedAtUtc)
            .Take(50)
            .ToListAsync(cancellationToken);

        foreach (var act in activities)
        {
            timeline.Add(new CaseTimelineItemDto(
                act.Id.ToString(),
                "InvestigationActivity",
                act.ActivityType.ToString(),
                $"{act.Description} - Outcome: {act.Outcome}",
                act.PerformedAtUtc,
                act.PerformedByUserId.ToString(),
                act.Location));
        }

        return timeline.OrderByDescending(t => t.TimestampUtc).ToList();
    }
}

// 4. Get Case Dashboard Metrics
public sealed record GetCaseDashboardMetricsQuery : IRequest<CaseDashboardMetricsDto>;

public class GetCaseDashboardMetricsQueryHandler : IRequestHandler<GetCaseDashboardMetricsQuery, CaseDashboardMetricsDto>
{
    private readonly IApplicationDbContext _context;

    public GetCaseDashboardMetricsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CaseDashboardMetricsDto> Handle(GetCaseDashboardMetricsQuery request, CancellationToken cancellationToken)
    {
        var caseMetrics = await _context.Cases
            .AsNoTracking()
            .GroupBy(c => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Active = g.Count(c => c.Status == CaseStatus.Open || c.Status == CaseStatus.UnderInvestigation),
                PendingReview = g.Count(c => c.Status == CaseStatus.PendingReview),
                CriticalPriority = g.Count(c => c.Priority == CasePriority.Critical),
                SlaBreached = g.Count(c => c.IsSlaBreached)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var overdueTasks = await _context.Tasks.CountAsync(t => t.DueDateUtc < DateTime.UtcNow && t.Status != TaskStatus.Completed && t.Status != TaskStatus.Cancelled, cancellationToken);
        var totalEvidence = await _context.Evidence.CountAsync(cancellationToken);

        var byType = await _context.Cases
            .GroupBy(c => c.Type)
            .Select(g => new { Key = g.Key.ToString(), Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

        var byStatus = await _context.Cases
            .GroupBy(c => c.Status)
            .Select(g => new { Key = g.Key.ToString(), Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

        var byRisk = await _context.Cases
            .GroupBy(c => c.CurrentRiskLevel)
            .Select(g => new { Key = g.Key.ToString(), Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

        return new CaseDashboardMetricsDto(
            caseMetrics?.Total ?? 0,
            caseMetrics?.Active ?? 0,
            caseMetrics?.PendingReview ?? 0,
            caseMetrics?.CriticalPriority ?? 0,
            caseMetrics?.SlaBreached ?? 0,
            overdueTasks,
            totalEvidence,
            byType,
            byStatus,
            byRisk);
    }
}
