using TraceCore.Domain.Common;
using TraceCore.Domain.Common.Exceptions;
using TraceCore.Domain.Enums;
using TraceCore.Domain.Events;
using TaskStatus = TraceCore.Domain.Enums.TaskStatus;

namespace TraceCore.Domain.Entities.Tasks;

public class CaseTask : BaseEntity, IAggregateRoot
{
    public Guid CaseId { get; private set; }
    public Guid? InvestigationId { get; private set; }
    public string Title { get; private set; } = default!;
    public string Description { get; private set; } = string.Empty;
    public Guid? AssignedToUserId { get; private set; }
    public TaskPriority Priority { get; private set; }
    public TaskStatus Status { get; private set; }
    public DateTime? DueDateUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public byte[] RowVersion { get; set; } = [];

    protected CaseTask() : base() { }

    public static CaseTask Create(
        Guid caseId,
        string title,
        string description,
        TaskPriority priority,
        Guid? assignedToUserId,
        DateTime? dueDateUtc,
        Guid? investigationId = null)
    {
        if (caseId == Guid.Empty)
            throw new DomainException("Case ID is required.");
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("Task title cannot be empty.");

        var task = new CaseTask
        {
            Id = Guid.NewGuid(),
            CaseId = caseId,
            InvestigationId = investigationId,
            Title = title.Trim(),
            Description = description?.Trim() ?? string.Empty,
            Priority = priority,
            Status = TaskStatus.Todo,
            AssignedToUserId = assignedToUserId,
            DueDateUtc = dueDateUtc,
            CreatedAtUtc = DateTime.UtcNow
        };

        task.AddDomainEvent(new TaskCreatedDomainEvent(
            task.Id,
            task.CaseId,
            task.Title,
            task.AssignedToUserId,
            task.DueDateUtc));

        return task;
    }

    public void UpdateStatus(TaskStatus newStatus, Guid userId)
    {
        if (Status == newStatus)
            return;

        bool isValid = (Status, newStatus) switch
        {
            (TaskStatus.Todo, TaskStatus.InProgress) => true,
            (TaskStatus.Todo, TaskStatus.Cancelled) => true,
            (TaskStatus.InProgress, TaskStatus.Blocked) => true,
            (TaskStatus.InProgress, TaskStatus.Completed) => true,
            (TaskStatus.InProgress, TaskStatus.Cancelled) => true,
            (TaskStatus.InProgress, TaskStatus.Todo) => true,
            (TaskStatus.Blocked, TaskStatus.InProgress) => true,
            (TaskStatus.Blocked, TaskStatus.Cancelled) => true,
            (TaskStatus.Completed, TaskStatus.InProgress) => true,
            _ => false
        };

        if (!isValid)
        {
            throw new InvalidStateTransitionException("Task", Status.ToString(), newStatus.ToString());
        }

        Status = newStatus;
        UpdatedAtUtc = DateTime.UtcNow;

        if (newStatus == TaskStatus.Completed)
        {
            CompletedAtUtc = DateTime.UtcNow;
            AddDomainEvent(new TaskCompletedDomainEvent(Id, CaseId, userId));
        }
        else
        {
            CompletedAtUtc = null;
        }
    }

    public void Assign(Guid userId)
    {
        AssignedToUserId = userId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public bool IsOverdue()
    {
        return DueDateUtc.HasValue &&
               DueDateUtc.Value < DateTime.UtcNow &&
               Status != TaskStatus.Completed &&
               Status != TaskStatus.Cancelled;
    }

    public void MarkOverdue()
    {
        if (IsOverdue())
        {
            AddDomainEvent(new TaskOverdueDomainEvent(Id, CaseId, Title, AssignedToUserId, DueDateUtc!.Value));
        }
    }
}
