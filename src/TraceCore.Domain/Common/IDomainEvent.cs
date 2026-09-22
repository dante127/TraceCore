namespace TraceCore.Domain.Common;

public interface IDomainEvent
{
    DateTime OccurredOnUtc { get; }
}

public abstract record BaseDomainEvent : IDomainEvent
{
    public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;
}
