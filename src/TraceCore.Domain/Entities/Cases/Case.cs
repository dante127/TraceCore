using TraceCore.Domain.Common;
using TraceCore.Domain.Common.Exceptions;
using TraceCore.Domain.Enums;
using TraceCore.Domain.Events;

namespace TraceCore.Domain.Entities.Cases;

public class Case : BaseEntity, IAggregateRoot
{
    public string CaseNumber { get; private set; } = default!;
    public string Title { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public CaseType Type { get; private set; }
    public CaseStatus Status { get; private set; }
    public CasePriority Priority { get; private set; }
    public ConfidentialityLevel ConfidentialityLevel { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public Guid? AssignedTeamId { get; private set; }
    public Guid? AssignedInvestigatorId { get; private set; }
    public DateTime? OpenedAtUtc { get; private set; }
    public DateTime? DueDateUtc { get; private set; }
    public DateTime? ClosedAtUtc { get; private set; }
    public int SlaTargetHours { get; private set; }
    public DateTime? SlaDeadlineUtc { get; private set; }
    public bool IsSlaBreached { get; private set; }
    public DateTime? SlaBreachedAtUtc { get; private set; }
    public int CurrentRiskScore { get; private set; }
    public RiskLevel CurrentRiskLevel { get; private set; }
    public byte[] RowVersion { get; set; } = [];

    private readonly List<CasePerson> _persons = [];
    public IReadOnlyCollection<CasePerson> Persons => _persons.AsReadOnly();

    private readonly List<CaseOrganization> _organizations = [];
    public IReadOnlyCollection<CaseOrganization> Organizations => _organizations.AsReadOnly();

    private readonly List<CaseAccessGrant> _accessGrants = [];
    public IReadOnlyCollection<CaseAccessGrant> AccessGrants => _accessGrants.AsReadOnly();

    // Required by EF Core
    protected Case() : base() { }

    private Case(
        string caseNumber,
        string title,
        string description,
        CaseType type,
        CasePriority priority,
        ConfidentialityLevel confidentiality,
        Guid createdByUserId,
        int slaTargetHours,
        DateTime? dueDateUtc,
        bool startAsOpen) : base()
    {
        if (string.IsNullOrWhiteSpace(caseNumber))
            throw new DomainException("Case number cannot be empty.");
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("Case title cannot be empty.");

        CaseNumber = caseNumber.Trim().ToUpperInvariant();
        Title = title.Trim();
        Description = description?.Trim() ?? string.Empty;
        Type = type;
        Priority = priority;
        ConfidentialityLevel = confidentiality;
        CreatedByUserId = createdByUserId;
        SlaTargetHours = slaTargetHours > 0 ? slaTargetHours : 168; // default 7 days
        DueDateUtc = dueDateUtc;
        CurrentRiskScore = 10;
        CurrentRiskLevel = RiskLevel.Low;

        if (startAsOpen)
        {
            Status = CaseStatus.Open;
            OpenedAtUtc = DateTime.UtcNow;
            SlaDeadlineUtc = DateTime.UtcNow.AddHours(SlaTargetHours);
        }
        else
        {
            Status = CaseStatus.Draft;
        }

        AddDomainEvent(new CaseCreatedDomainEvent(Id, CaseNumber, Title, Priority, ConfidentialityLevel));
    }

    public static Case CreateDraft(
        string caseNumber,
        string title,
        string description,
        CaseType type,
        CasePriority priority,
        ConfidentialityLevel confidentiality,
        Guid createdByUserId,
        int slaTargetHours,
        DateTime? dueDateUtc = null)
    {
        return new Case(caseNumber, title, description, type, priority, confidentiality, createdByUserId, slaTargetHours, dueDateUtc, false);
    }

    public static Case CreateOpen(
        string caseNumber,
        string title,
        string description,
        CaseType type,
        CasePriority priority,
        ConfidentialityLevel confidentiality,
        Guid createdByUserId,
        int slaTargetHours,
        DateTime? dueDateUtc = null)
    {
        return new Case(caseNumber, title, description, type, priority, confidentiality, createdByUserId, slaTargetHours, dueDateUtc, true);
    }

    public void TransitionStatus(CaseStatus newStatus, Guid changedByUserId, string reason)
    {
        if (Status == newStatus)
            return;

        bool isValid = (Status, newStatus) switch
        {
            (CaseStatus.Draft, CaseStatus.Open) => true,
            (CaseStatus.Open, CaseStatus.UnderInvestigation) => true,
            (CaseStatus.Open, CaseStatus.Closed) => true,
            (CaseStatus.UnderInvestigation, CaseStatus.PendingReview) => true,
            (CaseStatus.UnderInvestigation, CaseStatus.Open) => true,
            (CaseStatus.PendingReview, CaseStatus.Resolved) => true,
            (CaseStatus.PendingReview, CaseStatus.UnderInvestigation) => true,
            (CaseStatus.Resolved, CaseStatus.Closed) => true,
            (CaseStatus.Resolved, CaseStatus.Reopened) => true,
            (CaseStatus.Closed, CaseStatus.Reopened) => true,
            (CaseStatus.Reopened, CaseStatus.UnderInvestigation) => true,
            (CaseStatus.Reopened, CaseStatus.Open) => true,
            _ => false
        };

        if (!isValid)
        {
            throw new InvalidStateTransitionException("Case", Status.ToString(), newStatus.ToString(),
                $"Transition from {Status} to {newStatus} is not permitted by domain workflow rules.");
        }

        var oldStatus = Status;
        Status = newStatus;
        UpdatedAtUtc = DateTime.UtcNow;

        if (newStatus == CaseStatus.Open && OpenedAtUtc == null)
        {
            OpenedAtUtc = DateTime.UtcNow;
            SlaDeadlineUtc = DateTime.UtcNow.AddHours(SlaTargetHours);
        }
        else if (newStatus is CaseStatus.Closed or CaseStatus.Resolved)
        {
            ClosedAtUtc = DateTime.UtcNow;
        }
        else if (newStatus == CaseStatus.Reopened)
        {
            ClosedAtUtc = null;
        }

        AddDomainEvent(new CaseStatusChangedDomainEvent(Id, CaseNumber, oldStatus, newStatus, changedByUserId, reason));
    }

    public void UpdatePriority(CasePriority newPriority, Guid changedByUserId, string reason)
    {
        if (Priority == newPriority)
            return;

        var oldPriority = Priority;
        Priority = newPriority;
        UpdatedAtUtc = DateTime.UtcNow;

        AddDomainEvent(new CasePriorityChangedDomainEvent(Id, CaseNumber, oldPriority, newPriority, changedByUserId, reason));
    }

    public void AssignInvestigator(Guid investigatorId, Guid assignedByUserId, Guid? teamId = null)
    {
        if (AssignedInvestigatorId == investigatorId && AssignedTeamId == teamId)
            return;

        AssignedInvestigatorId = investigatorId;
        if (teamId.HasValue)
        {
            AssignedTeamId = teamId.Value;
        }

        UpdatedAtUtc = DateTime.UtcNow;
        AddDomainEvent(new CaseAssignedDomainEvent(Id, CaseNumber, investigatorId, assignedByUserId));
    }

    public void UpdateDetails(string title, string description, CaseType type, ConfidentialityLevel confidentiality)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("Case title cannot be empty.");

        Title = title.Trim();
        Description = description?.Trim() ?? string.Empty;
        Type = type;
        ConfidentialityLevel = confidentiality;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkSlaBreached(DateTime breachedAtUtc)
    {
        if (IsSlaBreached)
            return;

        IsSlaBreached = true;
        SlaBreachedAtUtc = breachedAtUtc;
        UpdatedAtUtc = DateTime.UtcNow;

        // Auto-escalate priority if currently low
        if (Priority == CasePriority.Low)
        {
            Priority = CasePriority.Medium;
        }

        AddDomainEvent(new CaseSlaBreachedDomainEvent(Id, CaseNumber, breachedAtUtc));
    }

    public void UpdateRiskAssessment(int score, RiskLevel level, string reason)
    {
        int clamped = Math.Clamp(score, 0, 100);
        var oldScore = CurrentRiskScore;
        var oldLevel = CurrentRiskLevel;

        CurrentRiskScore = clamped;
        CurrentRiskLevel = level;
        UpdatedAtUtc = DateTime.UtcNow;

        // Auto-escalation rule: if risk score >= 80 (Critical) and priority is less than High, escalate
        if (clamped >= 80 && Priority < CasePriority.High)
        {
            Priority = CasePriority.High;
        }

        if (oldScore != clamped || oldLevel != level)
        {
            AddDomainEvent(new CaseRiskLevelChangedDomainEvent(Id, CaseNumber, oldScore, clamped, oldLevel, level, reason));
        }
    }

    public void AddPerson(Guid personId, ParticipantRole role, string notes)
    {
        if (_persons.Any(p => p.PersonId == personId && p.Role == role))
            return;

        _persons.Add(new CasePerson(Id, personId, role, notes));
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void RemovePerson(Guid personId, ParticipantRole role)
    {
        var existing = _persons.FirstOrDefault(p => p.PersonId == personId && p.Role == role);
        if (existing != null)
        {
            _persons.Remove(existing);
            UpdatedAtUtc = DateTime.UtcNow;
        }
    }

    public void AddOrganization(Guid organizationId, string role, string notes)
    {
        if (_organizations.Any(o => o.OrganizationId == organizationId && o.Role == role))
            return;

        _organizations.Add(new CaseOrganization(Id, organizationId, role, notes));
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void AddAccessGrant(Guid userId, CaseAccessLevel accessLevel, Guid grantedByUserId, Guid? teamId = null)
    {
        var existing = _accessGrants.FirstOrDefault(g => g.UserId == userId);
        if (existing != null)
        {
            existing.UpdateAccessLevel(accessLevel);
        }
        else
        {
            _accessGrants.Add(new CaseAccessGrant(Id, userId, accessLevel, grantedByUserId, teamId));
        }
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void RevokeAccessGrant(Guid userId)
    {
        var grant = _accessGrants.FirstOrDefault(g => g.UserId == userId);
        if (grant != null)
        {
            _accessGrants.Remove(grant);
            UpdatedAtUtc = DateTime.UtcNow;
        }
    }
}
