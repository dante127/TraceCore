using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TraceCore.Application.Common.Exceptions;
using TraceCore.Application.Common.Interfaces;
using TraceCore.Domain.Entities.Cases;
using TraceCore.Domain.Entities.Risk;
using TraceCore.Domain.Enums;
using TaskStatus = TraceCore.Domain.Enums.TaskStatus;

namespace TraceCore.Application.Risk;

public sealed record RiskHistoryDto(
    Guid Id,
    Guid CaseId,
    int RiskScore,
    RiskLevel RiskLevel,
    DateTime EvaluatedAtUtc,
    string TriggerReason,
    Dictionary<string, int> Factors);

// 1. Service implementation
public class RiskAssessmentService : IRiskAssessmentService
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeProvider _dateTime;

    public RiskAssessmentService(IApplicationDbContext context, IDateTimeProvider dateTime)
    {
        _context = context;
        _dateTime = dateTime;
    }

    public async Task<RiskAssessmentResult> AssessCaseRiskAsync(
        Guid caseId,
        string triggerReason,
        CancellationToken cancellationToken = default)
    {
        var @case = await _context.Cases
            .FirstOrDefaultAsync(c => c.Id == caseId, cancellationToken)
            ?? throw new NotFoundException(nameof(Case), caseId);

        var factors = new Dictionary<string, int>();
        int totalScore = 0;

        // 1. Priority factor
        int priorityScore = @case.Priority switch
        {
            CasePriority.Critical => 30,
            CasePriority.High => 20,
            CasePriority.Medium => 10,
            _ => 5
        };
        factors["Priority"] = priorityScore;
        totalScore += priorityScore;

        // 2. Confidentiality factor
        int confScore = @case.ConfidentialityLevel switch
        {
            ConfidentialityLevel.TopSecret => 15,
            ConfidentialityLevel.Restricted => 10,
            ConfidentialityLevel.Confidential => 5,
            _ => 2
        };
        factors["Confidentiality"] = confScore;
        totalScore += confScore;

        // 3. Overdue tasks factor
        var now = _dateTime.UtcNow;
        var overdueCount = await _context.Tasks
            .CountAsync(t => t.CaseId == caseId &&
                             t.DueDateUtc.HasValue &&
                             t.DueDateUtc.Value < now &&
                             t.Status != TaskStatus.Completed &&
                             t.Status != TaskStatus.Cancelled, cancellationToken);
        int overdueScore = Math.Min(overdueCount * 10, 25);
        factors["OverdueTasks"] = overdueScore;
        totalScore += overdueScore;

        // 4. SLA Breach factor
        int slaScore = @case.IsSlaBreached ? 25 : 0;
        factors["SlaBreached"] = slaScore;
        totalScore += slaScore;

        // 5. Critical Evidence factor
        var criticalEvidenceCount = await _context.Evidence
            .CountAsync(e => e.CaseId == caseId && e.IsCritical, cancellationToken);
        int evidenceScore = Math.Min(criticalEvidenceCount * 10, 20);
        factors["CriticalEvidence"] = evidenceScore;
        totalScore += evidenceScore;

        // 6. Open Tasks load
        var openTasks = await _context.Tasks
            .CountAsync(t => t.CaseId == caseId &&
                             t.Status != TaskStatus.Completed &&
                             t.Status != TaskStatus.Cancelled, cancellationToken);
        int taskLoadScore = openTasks > 6 ? 10 : openTasks > 3 ? 5 : 0;
        factors["OpenTasksLoad"] = taskLoadScore;
        totalScore += taskLoadScore;

        // 7. Case Age factor (> 30 days)
        var ageDays = (now - @case.CreatedAtUtc).TotalDays;
        int ageScore = ageDays > 30 ? 10 : ageDays > 14 ? 5 : 0;
        factors["CaseAge"] = ageScore;
        totalScore += ageScore;

        // Clamp total score
        totalScore = Math.Clamp(totalScore, 0, 100);

        RiskLevel level = TraceCore.Domain.Entities.Cases.Case.RiskLevelForScore(totalScore);

        triggerReason = string.IsNullOrWhiteSpace(triggerReason) ? "Scheduled assessment" : triggerReason.Trim();

        // Update Case
        @case.UpdateRiskAssessment(totalScore, triggerReason);

        // Record History
        string factorsJson = JsonSerializer.Serialize(factors);
        var history = new RiskAssessmentHistory(caseId, totalScore, level, triggerReason, factorsJson);
        _context.Add(history);

        await _context.SaveChangesAsync(cancellationToken);

        string summary = $"Risk assessed at {level} ({totalScore}/100) triggered by: {triggerReason}.";
        return new RiskAssessmentResult(totalScore, level, factors, summary);
    }
}

// 2. Assess Case Risk Command
public sealed record AssessCaseRiskCommand(Guid CaseId, string TriggerReason = "Manual Request") : IRequest<RiskAssessmentResult>;

public class AssessCaseRiskCommandHandler : IRequestHandler<AssessCaseRiskCommand, RiskAssessmentResult>
{
    private readonly IRiskAssessmentService _riskService;

    public AssessCaseRiskCommandHandler(IRiskAssessmentService riskService)
    {
        _riskService = riskService;
    }

    public async Task<RiskAssessmentResult> Handle(AssessCaseRiskCommand request, CancellationToken cancellationToken)
    {
        return await _riskService.AssessCaseRiskAsync(request.CaseId, request.TriggerReason, cancellationToken);
    }
}

// 3. Get Case Risk History Query
public sealed record GetCaseRiskHistoryQuery(Guid CaseId) : IRequest<IReadOnlyList<RiskHistoryDto>>;

public class GetCaseRiskHistoryQueryHandler : IRequestHandler<GetCaseRiskHistoryQuery, IReadOnlyList<RiskHistoryDto>>
{
    private readonly IApplicationDbContext _context;

    public GetCaseRiskHistoryQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<RiskHistoryDto>> Handle(GetCaseRiskHistoryQuery request, CancellationToken cancellationToken)
    {
        var logs = await _context.RiskAssessmentHistories
            .AsNoTracking()
            .Where(r => r.CaseId == request.CaseId)
            .OrderByDescending(r => r.EvaluatedAtUtc)
            .ToListAsync(cancellationToken);

        return logs.Select(l => new RiskHistoryDto(
            l.Id,
            l.CaseId,
            l.RiskScore,
            l.RiskLevel,
            l.EvaluatedAtUtc,
            l.TriggerReason,
            JsonSerializer.Deserialize<Dictionary<string, int>>(l.FactorsJson) ?? new())).ToList();
    }
}
