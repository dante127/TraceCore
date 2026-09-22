using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TraceCore.Application.Common.Models;
using TraceCore.Application.Organizations;

namespace TraceCore.Api.Controllers;

[Authorize]
public class OrganizationsController : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedList<OrganizationDto>>> GetOrganizations([FromQuery] GetOrganizationsPagedQuery query, CancellationToken ct)
    {
        return Ok(await Sender.Send(query, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrganizationDto>> GetById(Guid id, CancellationToken ct)
    {
        return Ok(await Sender.Send(new GetOrganizationByIdQuery(id), ct));
    }

    [HttpPost]
    public async Task<ActionResult<Guid>> Create([FromBody] CreateOrganizationCommand command, CancellationToken ct)
    {
        var id = await Sender.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateOrganizationCommand command, CancellationToken ct)
    {
        if (id != command.Id)
            return BadRequest("Route ID does not match command ID.");

        await Sender.Send(command, ct);
        return NoContent();
    }

    [HttpPost("cases/link")]
    public async Task<IActionResult> LinkToCase([FromBody] LinkOrganizationToCaseCommand command, CancellationToken ct)
    {
        await Sender.Send(command, ct);
        return NoContent();
    }
}
