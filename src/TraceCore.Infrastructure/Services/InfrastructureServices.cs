using TraceCore.Application.Common.Interfaces;
using TraceCore.Domain.Entities.Notifications;
using TraceCore.Domain.Enums;

namespace TraceCore.Infrastructure.Services;

public class DateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}

public class NotificationService : INotificationService
{
    private readonly IApplicationDbContext _context;

    public NotificationService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task SendAsync(
        Guid userId,
        string title,
        string message,
        NotificationType type,
        string? refType = null,
        Guid? refId = null,
        CancellationToken cancellationToken = default)
    {
        var notification = new Notification(userId, title, message, type, refType, refId);
        _context.Add(notification);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task SendBatchAsync(
        IEnumerable<Guid> userIds,
        string title,
        string message,
        NotificationType type,
        string? refType = null,
        Guid? refId = null,
        CancellationToken cancellationToken = default)
    {
        foreach (var userId in userIds)
        {
            var notification = new Notification(userId, title, message, type, refType, refId);
            _context.Add(notification);
        }
        await _context.SaveChangesAsync(cancellationToken);
    }
}
