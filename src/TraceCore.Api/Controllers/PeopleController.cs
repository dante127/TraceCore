using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TraceCore.Api.Common;
using TraceCore.Api.Services;
using TraceCore.Application.Common.Models;
using TraceCore.Application.People;
using TraceCore.Domain.Enums;
using TraceCore.Infrastructure.Security;

namespace TraceCore.Api.Controllers;

[Authorize]
public class PeopleController : ApiControllerBase
{
    private readonly ICaseAuthorizationService _caseAuth;

    public PeopleController(ISender sender, ICaseAuthorizationService caseAuth)
        : base(sender)
    {
        _caseAuth = caseAuth;
    }

    [HttpGet]
    [HasPermission(Permissions.PeopleRead)]
    public async Task<ActionResult<PagedList<PersonDto>>> GetPeople([FromQuery] GetPeoplePagedQuery query, CancellationToken ct)
    {
        return Ok(await Sender.Send(query, ct));
    }

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.PeopleRead)]
    public async Task<ActionResult<PersonDto>> GetById(Guid id, CancellationToken ct)
    {
        return Ok(await Sender.Send(new GetPersonByIdQuery(id), ct));
    }

    [HttpPost]
    [HasPermission(Permissions.PeopleWrite)]
    public async Task<ActionResult<Guid>> Create([FromBody] CreatePersonCommand command, CancellationToken ct)
    {
        var id = await Sender.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.PeopleWrite)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePersonCommand command, CancellationToken ct)
    {
        if (id != command.Id)
            return BadRequest("Route ID does not match command ID.");

        await Sender.Send(command, ct);
        return NoContent();
    }

    [HttpPost("cases/link")]
    [HasPermission(Permissions.PeopleWrite)]
    public async Task<IActionResult> LinkToCase([FromBody] LinkPersonToCaseCommand command, CancellationToken ct)
    {
        if (!await _caseAuth.HasCaseAccessAsync(command.CaseId, CaseAccessLevel.ReadWrite, ct))
        {
            return Forbid();
        }

        await Sender.Send(command, ct);
        return NoContent();
    }

    [HttpPost("cases/remove")]
    [HasPermission(Permissions.PeopleWrite)]
    public async Task<IActionResult> RemoveFromCase([FromBody] RemovePersonFromCaseCommand command, CancellationToken ct)
    {
        if (!await _caseAuth.HasCaseAccessAsync(command.CaseId, CaseAccessLevel.ReadWrite, ct))
        {
            return Forbid();
        }

        await Sender.Send(command, ct);
        return NoContent();
    }
}