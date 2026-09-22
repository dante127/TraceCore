namespace TraceCore.Domain.Common.Exceptions;

public class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }

    public DomainException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public class InvalidStateTransitionException : DomainException
{
    public string CurrentState { get; }
    public string AttemptedState { get; }
    public string EntityName { get; }

    public InvalidStateTransitionException(string entityName, string currentState, string attemptedState)
        : base($"Invalid state transition for {entityName} from '{currentState}' to '{attemptedState}'.")
    {
        EntityName = entityName;
        CurrentState = currentState;
        AttemptedState = attemptedState;
    }

    public InvalidStateTransitionException(string entityName, string currentState, string attemptedState, string details)
        : base($"Invalid state transition for {entityName} from '{currentState}' to '{attemptedState}': {details}")
    {
        EntityName = entityName;
        CurrentState = currentState;
        AttemptedState = attemptedState;
    }
}

public class EntityNotFoundException : DomainException
{
    public string EntityName { get; }
    public object Key { get; }

    public EntityNotFoundException(string entityName, object key)
        : base($"Entity '{entityName}' with key '{key}' was not found.")
    {
        EntityName = entityName;
        Key = key;
    }
}

public class ConcurrencyConflictException : DomainException
{
    public string EntityName { get; }
    public object Key { get; }

    public ConcurrencyConflictException(string entityName, object key)
        : base($"The entity '{entityName}' ({key}) was modified or deleted by another concurrent transaction.")
    {
        EntityName = entityName;
        Key = key;
    }
}
