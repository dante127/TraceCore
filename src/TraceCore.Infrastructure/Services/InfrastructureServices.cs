using Microsoft.EntityFrameworkCore;
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

public class GraphTraversalService : IGraphTraversalService
{
    private readonly IApplicationDbContext _context;

    public GraphTraversalService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<GraphResult> GetEgoGraphAsync(
        Guid entityId,
        EntityType entityType,
        int maxDepth = 2,
        CancellationToken cancellationToken = default)
    {
        int depthLimit = Math.Clamp(maxDepth, 1, 3);
        var visited = new HashSet<Guid> { entityId };
        var frontier = new HashSet<Guid> { entityId };
        var edges = new List<GraphEdge>();

        for (int d = 0; d < depthLimit && frontier.Count > 0; d++)
        {
            var currentLevel = frontier.ToList();
            var relations = await _context.EntityRelationships
                .AsNoTracking()
                .Where(r => r.IsActive && (currentLevel.Contains(r.SourceEntityId) || currentLevel.Contains(r.TargetEntityId)))
                .ToListAsync(cancellationToken);

            frontier.Clear();

            foreach (var r in relations)
            {
                if (edges.All(e => e.Id != r.Id))
                {
                    edges.Add(new GraphEdge(
                        r.Id,
                        r.SourceEntityId,
                        r.SourceEntityType,
                        r.TargetEntityId,
                        r.TargetEntityType,
                        r.RelationshipType,
                        r.Description,
                        r.ConfidenceScore));
                }

                if (visited.Add(r.SourceEntityId))
                    frontier.Add(r.SourceEntityId);
                if (visited.Add(r.TargetEntityId))
                    frontier.Add(r.TargetEntityId);
            }
        }

        var nodes = new List<GraphNode>();

        var people = await _context.People
            .AsNoTracking()
            .Where(p => visited.Contains(p.Id))
            .ToListAsync(cancellationToken);
        foreach (var p in people)
        {
            nodes.Add(new GraphNode(p.Id, EntityType.Person, p.DisplayName, new Dictionary<string, object> { ["Email"] = p.Email ?? "" }));
        }

        var orgs = await _context.Organizations
            .AsNoTracking()
            .Where(o => visited.Contains(o.Id))
            .ToListAsync(cancellationToken);
        foreach (var o in orgs)
        {
            nodes.Add(new GraphNode(o.Id, EntityType.Organization, o.Name, new Dictionary<string, object> { ["Industry"] = o.Industry ?? "" }));
        }

        var cases = await _context.Cases
            .AsNoTracking()
            .Where(c => visited.Contains(c.Id))
            .ToListAsync(cancellationToken);
        foreach (var c in cases)
        {
            nodes.Add(new GraphNode(c.Id, EntityType.Case, c.CaseNumber + ": " + c.Title, new Dictionary<string, object> { ["Status"] = c.Status.ToString() }));
        }

        return new GraphResult(nodes, edges);
    }

    public async Task<IReadOnlyList<Guid>> FindShortestPathAsync(
        Guid sourceId,
        EntityType sourceType,
        Guid targetId,
        EntityType targetType,
        int maxDepth = 5,
        CancellationToken cancellationToken = default)
    {
        if (sourceId == targetId)
            return [sourceId];

        var queue = new Queue<List<Guid>>();
        queue.Enqueue([sourceId]);
        var visited = new HashSet<Guid> { sourceId };

        while (queue.Count > 0)
        {
            var path = queue.Dequeue();
            if (path.Count > maxDepth + 1)
                break;

            var current = path.Last();
            var neighbors = await _context.EntityRelationships
                .AsNoTracking()
                .Where(r => r.IsActive && (r.SourceEntityId == current || r.TargetEntityId == current))
                .Select(r => r.SourceEntityId == current ? r.TargetEntityId : r.SourceEntityId)
                .ToListAsync(cancellationToken);

            foreach (var neighbor in neighbors)
            {
                if (neighbor == targetId)
                {
                    var complete = new List<Guid>(path) { neighbor };
                    return complete;
                }

                if (visited.Add(neighbor))
                {
                    queue.Enqueue(new List<Guid>(path) { neighbor });
                }
            }
        }

        return [];
    }
}
