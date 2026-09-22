using TraceCore.Domain.Common;
using TraceCore.Domain.Common.Exceptions;
using TraceCore.Domain.Enums;
using TraceCore.Domain.Events;

namespace TraceCore.Domain.Entities.Investigations;

public class Investigation : BaseEntity, IAggregateRoot
{
    public Guid CaseId { get; private set; }
    public string Title { get; private set; } = default!;
    public Guid LeadInvestigatorId { get; private set; }
    public string Objectives { get; private set; } = string.Empty;
    public string Findings { get; private set; } = string.Empty;
    public InvestigationStatus Status { get; private set; }
    public DateTime? StartDateUtc { get; private set; }
    public DateTime? EndDateUtc { get; private set; }
    public byte[] RowVersion { get; set; } = [];

    private readonly List<InvestigationActivity> _activities = [];
    public IReadOnlyCollection<InvestigationActivity> Activities => _activities.AsReadOnly();

    protected Investigation() : base() { }

    public static Investigation Create(
        Guid caseId,
        string title,
        Guid leadInvestigatorId,
        string objectives,
        bool startImmediately = true)
    {
        if (caseId == Guid.Empty)
            throw new DomainException("Case ID is required.");
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("Investigation title cannot be empty.");
        if (leadInvestigatorId == Guid.Empty)
            throw new DomainException("Lead investigator is required.");

        var investigation = new Investigation
        {
            Id = Guid.NewGuid(),
            CaseId = caseId,
            Title = title.Trim(),
            LeadInvestigatorId = leadInvestigatorId,
            Objectives = objectives?.Trim() ?? string.Empty,
            Findings = string.Empty,
            Status = startImmediately ? InvestigationStatus.Active : InvestigationStatus.Planned,
            StartDateUtc = startImmediately ? DateTime.UtcNow : null,
            CreatedAtUtc = DateTime.UtcNow
        };

        if (startImmediately)
        {
            investigation.AddDomainEvent(new InvestigationStartedDomainEvent(
                investigation.Id,
                investigation.CaseId,
                investigation.LeadInvestigatorId));
        }

        return investigation;
    }

    public void Start(Guid leadInvestigatorId)
    {
        if (Status == InvestigationStatus.Active)
            return;

        Status = InvestigationStatus.Active;
        LeadInvestigatorId = leadInvestigatorId;
        StartDateUtc ??= DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;

        AddDomainEvent(new InvestigationStartedDomainEvent(Id, CaseId, leadInvestigatorId));
    }

    public InvestigationActivity AddActivity(
        ActivityType type,
        Guid performedByUserId,
        DateTime performedAtUtc,
        string description,
        string outcome,
        string location = "",
        string notes = "")
    {
        if (Status is InvestigationStatus.Completed or InvestigationStatus.Closed)
            throw new DomainException("Cannot log activities on a completed or closed investigation.");

        if (string.IsNullOrWhiteSpace(description))
            throw new DomainException("Activity description cannot be empty.");

        var activity = new InvestigationActivity(
            Id,
            type,
            performedByUserId,
            performedAtUtc,
            description,
            outcome,
            location,
            notes);

        _activities.Add(activity);
        UpdatedAtUtc = DateTime.UtcNow;

        return activity;
    }

    public void UpdateFindings(string findings)
    {
        Findings = findings?.Trim() ?? string.Empty;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Complete(string finalFindings)
    {
        if (Status == InvestigationStatus.Completed)
            return;

        Status = InvestigationStatus.Completed;
        EndDateUtc = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(finalFindings))
        {
            Findings = finalFindings.Trim();
        }
        UpdatedAtUtc = DateTime.UtcNow;

        AddDomainEvent(new InvestigationCompletedDomainEvent(Id, CaseId));
    }

    public void Suspend(string reason)
    {
        if (Status == InvestigationStatus.Suspended)
            return;

        Status = InvestigationStatus.Suspended;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Close()
    {
        Status = InvestigationStatus.Closed;
        EndDateUtc ??= DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}

public class InvestigationActivity : BaseEntity
{
    public Guid InvestigationId { get; private set; }
    public ActivityType ActivityType { get; private set; }
    public Guid PerformedByUserId { get; private set; }
    public DateTime PerformedAtUtc { get; private set; }
    public string Description { get; private set; } = default!;
    public string Outcome { get; private set; } = string.Empty;
    public string Location { get; private set; } = string.Empty;
    public string Notes { get; private set; } = string.Empty;

    protected InvestigationActivity() : base() { }

    public InvestigationActivity(
        Guid investigationId,
        ActivityType activityType,
        Guid performedByUserId,
        DateTime performedAtUtc,
        string description,
        string outcome,
        string location,
        string notes) : base()
    {
        InvestigationId = investigationId;
        ActivityType = activityType;
        PerformedByUserId = performedByUserId;
        PerformedAtUtc = performedAtUtc;
        Description = description.Trim();
        Outcome = outcome?.Trim() ?? string.Empty;
        Location = location?.Trim() ?? string.Empty;
        Notes = notes?.Trim() ?? string.Empty;
        CreatedAtUtc = DateTime.UtcNow;
    }
}
