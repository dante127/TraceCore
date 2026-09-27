using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TraceCore.Api.Common;
using TraceCore.Api.Services;
using TraceCore.Application.Relationships;
using TraceCore.Domain.Enums;

namespace TraceCore.Api.Controllers;

[Authorize]
[Route("api/v1/[controller]")]
public class RelationshipsController : ApiControllerBase
{
    public RelationshipsController(ISender sender)
        : base(sender)
    {
    }
    [HttpPost]
    [HasPermission(Permissions.RelationshipWrite)]
    public async Task<ActionResult<Guid>> CreateRelationship([FromBody] CreateEntityRelationshipCommand command, CancellationToken ct)
    {
        var id = await Sender.Send(command, ct);
        return Ok(id);
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.RelationshipWrite)]
    public async Task<IActionResult> DeactivateRelationship(Guid id, CancellationToken ct)
    {
        await Sender.Send(new DeactivateEntityRelationshipCommand(id), ct);
        return NoContent();
    }
}

[Authorize]
[Route("api/v1/[controller]")]
public class EntitiesController : ApiControllerBase
{
    public EntitiesController(ISender sender)
        : base(sender)
    {
    }
    [HttpGet("{entityType}/{id:guid}/relationships")]
    [HasPermission(Permissions.RelationshipRead)]
    public async Task<ActionResult<IReadOnlyList<RelationshipDto>>> GetDirectRelationships(
        EntityType entityType,
        Guid id,
        CancellationToken ct)
    {
        return Ok(await Sender.Send(new GetDirectRelationshipsQuery(id, entityType), ct));
    }

    [HttpGet("{entityType}/{id:guid}/graph")]
    [HasPermission(Permissions.RelationshipRead)]
    public async Task<ActionResult<GraphResultDto>> GetEntityGraph(
        EntityType entityType,
        Guid id,
        [FromQuery] int depth = 2,
        CancellationToken ct = default)
    {
        return Ok(await Sender.Send(new GetEntityGraphQuery(id, entityType, depth), ct));
    }

    [HttpGet("paths")]
    [HasPermission(Permissions.RelationshipRead)]
    public async Task<ActionResult<GraphPathDto>> FindPath(
        [FromQuery] Guid sourceId,
        [FromQuery] EntityType sourceType,
        [FromQuery] Guid targetId,
        [FromQuery] EntityType targetType,
        [FromQuery] int maxDepth = 5,
        CancellationToken ct = default)
    {
        return Ok(await Sender.Send(new FindRelationshipPathQuery(sourceId, sourceType, targetId, targetType, maxDepth), ct));
    }
}
