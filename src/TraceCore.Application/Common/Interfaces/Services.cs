using TraceCore.Domain.Enums;

namespace TraceCore.Application.Common.Interfaces;

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default);
}

public interface IFileStorage
{
    Task<string> SaveFileAsync(Stream fileStream, string fileName, string contentType, CancellationToken cancellationToken = default);
    Task<Stream> GetFileStreamAsync(string storagePath, CancellationToken cancellationToken = default);
    Task DeleteFileAsync(string storagePath, CancellationToken cancellationToken = default);
    Task<string> ComputeSha256Async(Stream fileStream, CancellationToken cancellationToken = default);
}

public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Email { get; }
    string? DisplayName { get; }
    IReadOnlyList<string> Roles { get; }
    IReadOnlyList<string> Permissions { get; }
    bool IsAuthenticated { get; }
    bool HasPermission(string permission);
    bool IsInRole(string role);
}

public interface INotificationService
{
    Task SendAsync(Guid userId, string title, string message, NotificationType type, string? refType = null, Guid? refId = null, CancellationToken cancellationToken = default);
    Task SendBatchAsync(IEnumerable<Guid> userIds, string title, string message, NotificationType type, string? refType = null, Guid? refId = null, CancellationToken cancellationToken = default);
}

public sealed record RiskAssessmentResult(
    int TotalScore,
    RiskLevel Level,
    Dictionary<string, int> FactorScores,
    string Summary);

public interface IRiskAssessmentService
{
    Task<RiskAssessmentResult> AssessCaseRiskAsync(Guid caseId, string triggerReason, CancellationToken cancellationToken = default);
}

public interface ISlaCalculationService
{
    DateTime CalculateDeadline(CasePriority priority, CaseType type, DateTime fromUtc);
    int GetTargetHours(CasePriority priority, CaseType type);
    bool IsBreached(DateTime deadlineUtc, DateTime currentUtc);
    double CalculateRemainingHours(DateTime deadlineUtc, DateTime currentUtc);
}

public sealed record GraphNode(Guid Id, EntityType Type, string Label, Dictionary<string, object> Attributes);
public sealed record GraphEdge(Guid Id, Guid SourceId, EntityType SourceType, Guid TargetId, EntityType TargetType, RelationshipType RelationshipType, string Description, float Confidence);
public sealed record GraphResult(IReadOnlyList<GraphNode> Nodes, IReadOnlyList<GraphEdge> Edges);

public interface IGraphTraversalService
{
    Task<GraphResult> GetEgoGraphAsync(Guid entityId, EntityType entityType, int maxDepth = 2, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> FindShortestPathAsync(Guid sourceId, EntityType sourceType, Guid targetId, EntityType targetType, int maxDepth = 5, CancellationToken cancellationToken = default);
}

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
