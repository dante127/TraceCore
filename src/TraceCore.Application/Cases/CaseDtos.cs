using TraceCore.Domain.Enums;

namespace TraceCore.Application.Cases;

public sealed record CaseDto(
    Guid Id,
    string CaseNumber,
    string Title,
    CaseType Type,
    CaseStatus Status,
    CasePriority Priority,
    ConfidentialityLevel ConfidentialityLevel,
    Guid CreatedByUserId,
    Guid? AssignedTeamId,
    Guid? AssignedInvestigatorId,
    DateTime? OpenedAtUtc,
    DateTime? DueDateUtc,
    DateTime? ClosedAtUtc,
    int SlaTargetHours,
    DateTime? SlaDeadlineUtc,
    bool IsSlaBreached,
    int CurrentRiskScore,
    RiskLevel CurrentRiskLevel,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    string RowVersion);

public sealed record CaseParticipantDto(
    Guid Id,
    Guid PersonId,
    string DisplayName,
    string Email,
    ParticipantRole Role,
    string Notes,
    DateTime AssignedAtUtc);

public sealed record CaseOrganizationDto(
    Guid Id,
    Guid OrganizationId,
    string OrganizationName,
    string Role,
    string Notes,
    DateTime AssignedAtUtc);

public sealed record CaseDetailDto(
    Guid Id,
    string CaseNumber,
    string Title,
    string Description,
    CaseType Type,
    CaseStatus Status,
    CasePriority Priority,
    ConfidentialityLevel ConfidentialityLevel,
    Guid CreatedByUserId,
    Guid? AssignedTeamId,
    Guid? AssignedInvestigatorId,
    DateTime? OpenedAtUtc,
    DateTime? DueDateUtc,
    DateTime? ClosedAtUtc,
    int SlaTargetHours,
    DateTime? SlaDeadlineUtc,
    bool IsSlaBreached,
    DateTime? SlaBreachedAtUtc,
    int CurrentRiskScore,
    RiskLevel CurrentRiskLevel,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    string RowVersion,
    IReadOnlyList<CaseParticipantDto> Participants,
    IReadOnlyList<CaseOrganizationDto> Organizations,
    int OpenTaskCount,
    int EvidenceCount,
    int InvestigationCount);

public sealed record CaseTimelineItemDto(
    string Id,
    string Category,
    string Title,
    string Description,
    DateTime TimestampUtc,
    string PerformedBy,
    string? Meta);

public sealed record CaseDashboardMetricsDto(
    int TotalCases,
    int ActiveCases,
    int PendingReviewCases,
    int CriticalPriorityCases,
    int SlaBreachedCases,
    int OverdueTasks,
    int TotalEvidenceCount,
    Dictionary<string, int> CasesByType,
    Dictionary<string, int> CasesByStatus,
    Dictionary<string, int> CasesByRiskLevel);
