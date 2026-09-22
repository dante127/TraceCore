using TraceCore.Domain.Common;

namespace TraceCore.Domain.Entities.Audit;

public class AuditLog : BaseEntity
{
    public Guid? UserId { get; private set; }
    public string Action { get; private set; } = default!;
    public string EntityType { get; private set; } = default!;
    public string EntityId { get; private set; } = default!;
    public DateTime TimestampUtc { get; private set; } = DateTime.UtcNow;
    public string? IpAddress { get; private set; }
    public string? CorrelationId { get; private set; }
    public string? BeforeJson { get; private set; }
    public string? AfterJson { get; private set; }

    protected AuditLog() : base() { }

    public AuditLog(
        Guid? userId,
        string action,
        string entityType,
        string entityId,
        string? ipAddress = null,
        string? correlationId = null,
        string? beforeJson = null,
        string? afterJson = null) : base()
    {
        UserId = userId;
        Action = action;
        EntityType = entityType;
        EntityId = entityId;
        TimestampUtc = DateTime.UtcNow;
        IpAddress = ipAddress;
        CorrelationId = correlationId;
        BeforeJson = beforeJson;
        AfterJson = afterJson;
        CreatedAtUtc = TimestampUtc;
    }
}
