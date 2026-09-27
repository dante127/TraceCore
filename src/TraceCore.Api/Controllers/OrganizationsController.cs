using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TraceCore.Api.Common;
using TraceCore.Api.Services;
using TraceCore.Application.Common.Models;
using TraceCore.Application.Organizations;
using TraceCore.Domain.Enums;
using TraceCore.Infrastructure.Security;

namespace TraceCore.Api.Controllers;

[Authorize]
public class OrganizationsController : ApiControllerBase
{
    private readonly ICaseAuthorizationService _caseAuth;

    public OrganizationsController(ISender sender, ICaseAuthorizationService caseAuth)
        : base(sender)
    {
        _caseAuth = caseAuth;
    }

    [HttpGet]
    [HasPermission(Permissions.OrganizationRead)]
    public async Task<ActionResult<PagedList<OrganizationDto>>> GetOrganizations([FromQuery] GetOrganizationsPagedQuery query, CancellationToken ct)
    {
        return Ok(await Sender.Send(query, ct));
    }

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.OrganizationRead)]
    public async Task<ActionResult<OrganizationDto>> GetById(Guid id, CancellationToken ct)
    {
        return Ok(await Sender.Send(new GetOrganizationByIdQuery(id), ct));
    }

    [HttpPost]
    [HasPermission(Permissions.OrganizationWrite)]
    public async Task<ActionResult<Guid>> Create([FromBody] CreateOrganizationCommand command, CancellationToken ct)
    {
        var id = await Sender.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.OrganizationWrite)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateOrganizationCommand command, CancellationToken ct)
    {
        if (id != command.Id)
            return BadRequest("Route ID does not match command ID.");

        await Sender.Send(command, ct);
        return NoContent();
    }

    [HttpPost("cases/link")]
    [HasPermission(Permissions.OrganizationWrite)]
    public async Task<IActionResult> LinkToCase([FromBody] LinkOrganizationToCaseCommand command, CancellationToken ct)
    {
        if (!await _caseAuth.HasCaseAccessAsync(command.CaseId, CaseAccessLevel.ReadWrite, ct))
        {
            return Forbid();
        }

        await Sender.Send(command, ct);
        return NoContent();
    }
}
