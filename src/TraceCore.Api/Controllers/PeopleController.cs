using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TraceCore.Application.Common.Models;
using TraceCore.Application.People;

namespace TraceCore.Api.Controllers;

[Authorize]
public class PeopleController : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedList<PersonDto>>> GetPeople([FromQuery] GetPeoplePagedQuery query, CancellationToken ct)
    {
        return Ok(await Sender.Send(query, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PersonDto>> GetById(Guid id, CancellationToken ct)
    {
        return Ok(await Sender.Send(new GetPersonByIdQuery(id), ct));
    }

    [HttpPost]
    public async Task<ActionResult<Guid>> Create([FromBody] CreatePersonCommand command, CancellationToken ct)
    {
        var id = await Sender.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePersonCommand command, CancellationToken ct)
    {
        if (id != command.Id)
            return BadRequest("Route ID does not match command ID.");

        await Sender.Send(command, ct);
        return NoContent();
    }

    [HttpPost("cases/link")]
    public async Task<IActionResult> LinkToCase([FromBody] LinkPersonToCaseCommand command, CancellationToken ct)
    {
        await Sender.Send(command, ct);
        return NoContent();
    }

    [HttpPost("cases/remove")]
    public async Task<IActionResult> RemoveFromCase([FromBody] RemovePersonFromCaseCommand command, CancellationToken ct)
    {
        await Sender.Send(command, ct);
        return NoContent();
    }
}
