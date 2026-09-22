using TraceCore.Domain.Common;
using TraceCore.Domain.Enums;

namespace TraceCore.Domain.Entities.Notifications;

public class Notification : BaseEntity
{
    public Guid UserId { get; private set; }
    public string Title { get; private set; } = default!;
    public string Message { get; private set; } = default!;
    public NotificationType Type { get; private set; }
    public string? ReferenceType { get; private set; }
    public Guid? ReferenceId { get; private set; }
    public bool IsRead { get; private set; }
    public DateTime? ReadAtUtc { get; private set; }

    protected Notification() : base() { }

    public Notification(
        Guid userId,
        string title,
        string message,
        NotificationType type,
        string? referenceType = null,
        Guid? referenceId = null) : base()
    {
        UserId = userId;
        Title = title?.Trim() ?? string.Empty;
        Message = message?.Trim() ?? string.Empty;
        Type = type;
        ReferenceType = referenceType;
        ReferenceId = referenceId;
        IsRead = false;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void MarkAsRead()
    {
        if (IsRead) return;
        IsRead = true;
        ReadAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
