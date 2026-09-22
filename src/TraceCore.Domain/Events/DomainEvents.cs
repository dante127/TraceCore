using TraceCore.Domain.Common;
using TraceCore.Domain.Enums;

namespace TraceCore.Domain.Events;

public sealed record CaseCreatedDomainEvent(
    Guid CaseId,
    string CaseNumber,
    string Title,
    CasePriority Priority,
    ConfidentialityLevel Confidentiality) : BaseDomainEvent;

public sealed record CaseStatusChangedDomainEvent(
    Guid CaseId,
    string CaseNumber,
    CaseStatus OldStatus,
    CaseStatus NewStatus,
    Guid ChangedByUserId,
    string Reason) : BaseDomainEvent;

public sealed record CasePriorityChangedDomainEvent(
    Guid CaseId,
    string CaseNumber,
    CasePriority OldPriority,
    CasePriority NewPriority,
    Guid ChangedByUserId,
    string Reason) : BaseDomainEvent;

public sealed record CaseAssignedDomainEvent(
    Guid CaseId,
    string CaseNumber,
    Guid InvestigatorId,
    Guid AssignedByUserId) : BaseDomainEvent;

public sealed record CaseSlaBreachedDomainEvent(
    Guid CaseId,
    string CaseNumber,
    DateTime BreachedAtUtc) : BaseDomainEvent;

public sealed record CaseRiskLevelChangedDomainEvent(
    Guid CaseId,
    string CaseNumber,
    int OldScore,
    int NewScore,
    RiskLevel OldLevel,
    RiskLevel NewLevel,
    string Reason) : BaseDomainEvent;

public sealed record EvidenceAddedDomainEvent(
    Guid EvidenceId,
    Guid CaseId,
    string EvidenceNumber,
    EvidenceType Type,
    bool IsCritical) : BaseDomainEvent;

public sealed record EvidenceCustodyTransferredDomainEvent(
    Guid EvidenceId,
    Guid CaseId,
    Guid FromUserId,
    Guid ToUserId,
    CustodyAction Action,
    string Location,
    string CurrentHash) : BaseDomainEvent;

public sealed record EvidenceArchivedDomainEvent(
    Guid EvidenceId,
    Guid CaseId,
    Guid ArchivedByUserId,
    string Reason) : BaseDomainEvent;

public sealed record InvestigationStartedDomainEvent(
    Guid InvestigationId,
    Guid CaseId,
    Guid LeadInvestigatorId) : BaseDomainEvent;

public sealed record InvestigationCompletedDomainEvent(
    Guid InvestigationId,
    Guid CaseId) : BaseDomainEvent;

public sealed record TaskCreatedDomainEvent(
    Guid TaskId,
    Guid CaseId,
    string Title,
    Guid? AssignedToUserId,
    DateTime? DueDateUtc) : BaseDomainEvent;

public sealed record TaskCompletedDomainEvent(
    Guid TaskId,
    Guid CaseId,
    Guid CompletedByUserId) : BaseDomainEvent;

public sealed record TaskOverdueDomainEvent(
    Guid TaskId,
    Guid CaseId,
    string Title,
    Guid? AssignedToUserId,
    DateTime DueDateUtc) : BaseDomainEvent;
