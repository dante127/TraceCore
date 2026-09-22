using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TraceCore.Application.Investigations;
using TraceCore.Domain.Enums;
using TraceCore.Infrastructure.Security;

namespace TraceCore.Api.Controllers;

[Authorize]
public class InvestigationsController : ApiControllerBase
{
    private readonly ICaseAuthorizationService _caseAuth;

    public InvestigationsController(ICaseAuthorizationService caseAuth)
    {
        _caseAuth = caseAuth;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<InvestigationDto>>> GetInvestigations([FromQuery] Guid caseId, CancellationToken ct)
    {
        if (!await _caseAuth.HasCaseAccessAsync(caseId, CaseAccessLevel.Read, ct))
        {
            return Forbid();
        }

        return Ok(await Sender.Send(new GetInvestigationsByCaseIdQuery(caseId), ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<InvestigationDetailDto>> GetById(Guid id, CancellationToken ct)
    {
        var inv = await Sender.Send(new GetInvestigationByIdQuery(id), ct);
        if (!await _caseAuth.HasCaseAccessAsync(inv.CaseId, CaseAccessLevel.Read, ct))
        {
            return Forbid();
        }

        return Ok(inv);
    }

    [HttpPost]
    public async Task<ActionResult<Guid>> Create([FromBody] CreateInvestigationCommand command, CancellationToken ct)
    {
        if (!await _caseAuth.HasCaseAccessAsync(command.CaseId, CaseAccessLevel.ReadWrite, ct))
        {
            return Forbid();
        }

        var id = await Sender.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [HttpPost("{id:guid}/activities")]
    public async Task<ActionResult<Guid>> AddActivity(Guid id, [FromBody] AddInvestigationActivityCommand command, CancellationToken ct)
    {
        if (id != command.InvestigationId)
            return BadRequest("Route ID does not match command ID.");

        var activityId = await Sender.Send(command, ct);
        return Ok(activityId);
    }

    [HttpPut("{id:guid}/findings")]
    public async Task<IActionResult> UpdateFindings(Guid id, [FromBody] UpdateInvestigationFindingsCommand command, CancellationToken ct)
    {
        if (id != command.InvestigationId)
            return BadRequest("Route ID does not match command ID.");

        await Sender.Send(command, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateInvestigationStatusCommand command, CancellationToken ct)
    {
        if (id != command.InvestigationId)
            return BadRequest("Route ID does not match command ID.");

        await Sender.Send(command, ct);
        return NoContent();
    }
}
