using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TraceCore.Application.Common.Exceptions;
using TraceCore.Application.Common.Interfaces;
using TraceCore.Domain.Entities.Relationships;
using TraceCore.Domain.Enums;

namespace TraceCore.Application.Relationships;

public sealed record RelationshipDto(
    Guid Id,
    Guid SourceEntityId,
    EntityType SourceEntityType,
    Guid TargetEntityId,
    EntityType TargetEntityType,
    RelationshipType RelationshipType,
    string Description,
    float ConfidenceScore,
    DateTime? StartDateUtc,
    DateTime? EndDateUtc,
    bool IsActive,
    DateTime CreatedAtUtc);

public sealed record GraphNodeDto(
    Guid Id,
    EntityType Type,
    string Label,
    Dictionary<string, object> Attributes);

public sealed record GraphEdgeDto(
    Guid Id,
    Guid SourceId,
    EntityType SourceType,
    Guid TargetId,
    EntityType TargetType,
    RelationshipType RelationshipType,
    string Description,
    float Confidence);

public sealed record GraphResultDto(
    IReadOnlyList<GraphNodeDto> Nodes,
    IReadOnlyList<GraphEdgeDto> Edges);

public sealed record GraphPathDto(
    bool PathFound,
    IReadOnlyList<Guid> PathEntityIds,
    int PathLength);

// 1. Create Relationship
public sealed record CreateEntityRelationshipCommand(
    Guid SourceEntityId,
    EntityType SourceEntityType,
    Guid TargetEntityId,
    EntityType TargetEntityType,
    RelationshipType RelationshipType,
    string Description = "",
    float ConfidenceScore = 1.0f,
    DateTime? StartDateUtc = null,
    DateTime? EndDateUtc = null) : IRequest<Guid>;

public class CreateEntityRelationshipCommandValidator : AbstractValidator<CreateEntityRelationshipCommand>
{
    public CreateEntityRelationshipCommandValidator()
    {
        RuleFor(v => v.SourceEntityId).NotEmpty();
        RuleFor(v => v.TargetEntityId).NotEmpty();
        RuleFor(v => v.SourceEntityType).IsInEnum();
        RuleFor(v => v.TargetEntityType).IsInEnum();
        RuleFor(v => v.RelationshipType).IsInEnum();
        RuleFor(v => v.ConfidenceScore).InclusiveBetween(0.0f, 1.0f);
    }
}

public class CreateEntityRelationshipCommandHandler : IRequestHandler<CreateEntityRelationshipCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public CreateEntityRelationshipCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreateEntityRelationshipCommand request, CancellationToken cancellationToken)
    {
        var rel = new EntityRelationship(
            request.SourceEntityId,
            request.SourceEntityType,
            request.TargetEntityId,
            request.TargetEntityType,
            request.RelationshipType,
            request.Description,
            request.ConfidenceScore,
            request.StartDateUtc,
            request.EndDateUtc);

        _context.Add(rel);
        await _context.SaveChangesAsync(cancellationToken);

        return rel.Id;
    }
}

// 2. Deactivate Relationship
public sealed record DeactivateEntityRelationshipCommand(Guid Id) : IRequest<Unit>;

public class DeactivateEntityRelationshipCommandHandler : IRequestHandler<DeactivateEntityRelationshipCommand, Unit>
{
    private readonly IApplicationDbContext _context;

    public DeactivateEntityRelationshipCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Unit> Handle(DeactivateEntityRelationshipCommand request, CancellationToken cancellationToken)
    {
        var rel = await _context.EntityRelationships
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(EntityRelationship), request.Id);

        rel.Deactivate();
        await _context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

// 3. Get Direct Relationships
public sealed record GetDirectRelationshipsQuery(Guid EntityId, EntityType EntityType) : IRequest<IReadOnlyList<RelationshipDto>>;

public class GetDirectRelationshipsQueryHandler : IRequestHandler<GetDirectRelationshipsQuery, IReadOnlyList<RelationshipDto>>
{
    private readonly IApplicationDbContext _context;

    public GetDirectRelationshipsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<RelationshipDto>> Handle(GetDirectRelationshipsQuery request, CancellationToken cancellationToken)
    {
        return await _context.EntityRelationships
            .AsNoTracking()
            .Where(r => r.IsActive &&
                       ((r.SourceEntityId == request.EntityId && r.SourceEntityType == request.EntityType) ||
                        (r.TargetEntityId == request.EntityId && r.TargetEntityType == request.EntityType)))
            .Select(r => new RelationshipDto(
                r.Id,
                r.SourceEntityId,
                r.SourceEntityType,
                r.TargetEntityId,
                r.TargetEntityType,
                r.RelationshipType,
                r.Description,
                r.ConfidenceScore,
                r.StartDateUtc,
                r.EndDateUtc,
                r.IsActive,
                r.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }
}

// 4. Get Entity Graph (Ego Graph)
public sealed record GetEntityGraphQuery(Guid EntityId, EntityType EntityType, int Depth = 2) : IRequest<GraphResultDto>;

public class GetEntityGraphQueryHandler : IRequestHandler<GetEntityGraphQuery, GraphResultDto>
{
    private readonly IApplicationDbContext _context;

    public GetEntityGraphQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<GraphResultDto> Handle(GetEntityGraphQuery request, CancellationToken cancellationToken)
    {
        int maxDepth = Math.Clamp(request.Depth, 1, 3);
        var visitedEntityIds = new HashSet<Guid> { request.EntityId };
        var frontier = new HashSet<Guid> { request.EntityId };
        var edges = new List<GraphEdgeDto>();

        for (int d = 0; d < maxDepth && frontier.Count > 0; d++)
        {
            var currentLevel = frontier.ToList();
            var levelRels = await _context.EntityRelationships
                .AsNoTracking()
                .Where(r => r.IsActive && (currentLevel.Contains(r.SourceEntityId) || currentLevel.Contains(r.TargetEntityId)))
                .ToListAsync(cancellationToken);

            frontier.Clear();

            foreach (var r in levelRels)
            {
                if (edges.All(e => e.Id != r.Id))
                {
                    edges.Add(new GraphEdgeDto(
                        r.Id,
                        r.SourceEntityId,
                        r.SourceEntityType,
                        r.TargetEntityId,
                        r.TargetEntityType,
                        r.RelationshipType,
                        r.Description,
                        r.ConfidenceScore));
                }

                if (visitedEntityIds.Add(r.SourceEntityId))
                {
                    frontier.Add(r.SourceEntityId);
                }

                if (visitedEntityIds.Add(r.TargetEntityId))
                {
                    frontier.Add(r.TargetEntityId);
                }
            }
        }

        // Fetch labels for visited entities
        var nodes = new List<GraphNodeDto>();

        // People
        var people = await _context.People
            .AsNoTracking()
            .Where(p => visitedEntityIds.Contains(p.Id))
            .ToListAsync(cancellationToken);
        foreach (var p in people)
        {
            nodes.Add(new GraphNodeDto(p.Id, EntityType.Person, p.DisplayName, new Dictionary<string, object> { ["Email"] = p.Email ?? "", ["Phone"] = p.Phone ?? "" }));
        }

        // Orgs
        var orgs = await _context.Organizations
            .AsNoTracking()
            .Where(o => visitedEntityIds.Contains(o.Id))
            .ToListAsync(cancellationToken);
        foreach (var o in orgs)
        {
            nodes.Add(new GraphNodeDto(o.Id, EntityType.Organization, o.Name, new Dictionary<string, object> { ["Industry"] = o.Industry ?? "" }));
        }

        // Cases
        var cases = await _context.Cases
            .AsNoTracking()
            .Where(c => visitedEntityIds.Contains(c.Id))
            .ToListAsync(cancellationToken);
        foreach (var c in cases)
        {
            nodes.Add(new GraphNodeDto(c.Id, EntityType.Case, $"{c.CaseNumber}: {c.Title}", new Dictionary<string, object> { ["Status"] = c.Status.ToString(), ["Priority"] = c.Priority.ToString() }));
        }

        return new GraphResultDto(nodes, edges);
    }
}

// 5. Find Relationship Path (BFS)
public sealed record FindRelationshipPathQuery(
    Guid SourceId,
    EntityType SourceType,
    Guid TargetId,
    EntityType TargetType,
    int MaxDepth = 5) : IRequest<GraphPathDto>;

public class FindRelationshipPathQueryHandler : IRequestHandler<FindRelationshipPathQuery, GraphPathDto>
{
    private readonly IApplicationDbContext _context;

    public FindRelationshipPathQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<GraphPathDto> Handle(FindRelationshipPathQuery request, CancellationToken cancellationToken)
    {
        if (request.SourceId == request.TargetId)
        {
            return new GraphPathDto(true, [request.SourceId], 0);
        }

        int maxDepth = Math.Clamp(request.MaxDepth, 1, 5);

        // BFS path finding
        var queue = new Queue<List<Guid>>();
        queue.Enqueue([request.SourceId]);

        var visited = new HashSet<Guid> { request.SourceId };

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
                if (neighbor == request.TargetId)
                {
                    var completePath = new List<Guid>(path) { neighbor };
                    return new GraphPathDto(true, completePath, completePath.Count - 1);
                }

                if (visited.Add(neighbor))
                {
                    var newPath = new List<Guid>(path) { neighbor };
                    queue.Enqueue(newPath);
                }
            }
        }

        return new GraphPathDto(false, [], -1);
    }
}
