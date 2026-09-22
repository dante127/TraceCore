using TraceCore.Domain.Common;

namespace TraceCore.Domain.Entities.Outbox;

public class OutboxMessage : BaseEntity
{
    public DateTime OccurredOnUtc { get; private set; }
    public string Type { get; private set; } = default!;
    public string ContentJson { get; private set; } = default!;
    public DateTime? ProcessedOnUtc { get; private set; }
    public string? Error { get; private set; }
    public int RetryCount { get; private set; }

    protected OutboxMessage() : base() { }

    public OutboxMessage(DateTime occurredOnUtc, string type, string contentJson) : base()
    {
        OccurredOnUtc = occurredOnUtc;
        Type = type;
        ContentJson = contentJson;
        RetryCount = 0;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void MarkProcessed()
    {
        ProcessedOnUtc = DateTime.UtcNow;
        Error = null;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkFailed(string error)
    {
        RetryCount++;
        Error = error;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
