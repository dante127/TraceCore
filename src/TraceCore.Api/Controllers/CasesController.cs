using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TraceCore.Api.Common;
using TraceCore.Application.Cases;
using TraceCore.Application.Cases.Commands;
using TraceCore.Application.Cases.Queries;
using TraceCore.Application.Common.Models;
using TraceCore.Domain.Enums;
using TraceCore.Infrastructure.Security;

namespace TraceCore.Api.Controllers;

[Authorize]
public class CasesController : ApiControllerBase
{
    private readonly ICaseAuthorizationService _caseAuth;

    public CasesController(ICaseAuthorizationService caseAuth)
    {
        _caseAuth = caseAuth;
    }

    [HttpGet]
    public async Task<ActionResult<PagedList<CaseDto>>> GetCases([FromQuery] GetCasesPagedQuery query, CancellationToken ct)
    {
        return Ok(await Sender.Send(query, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CaseDetailDto>> GetCaseById(Guid id, CancellationToken ct)
    {
        if (!await _caseAuth.HasCaseAccessAsync(id, CaseAccessLevel.Read, ct))
        {
            return Forbid();
        }

        return Ok(await Sender.Send(new GetCaseByIdQuery(id), ct));
    }

    [HttpPost]
    public async Task<ActionResult<CreateCaseResult>> CreateCase([FromBody] CreateCaseCommand command, CancellationToken ct)
    {
        var result = await Sender.Send(command, ct);
        return CreatedAtAction(nameof(GetCaseById), new { id = result.CaseId }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateCase(Guid id, [FromBody] UpdateCaseCommand command, CancellationToken ct)
    {
        if (id != command.Id)
            return BadRequest("Route ID does not match payload ID.");

        if (!await _caseAuth.HasCaseAccessAsync(id, CaseAccessLevel.ReadWrite, ct))
        {
            return Forbid();
        }

        await Sender.Send(command, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/status")]
    public async Task<IActionResult> ChangeStatus(Guid id, [FromBody] ChangeCaseStatusCommand command, CancellationToken ct)
    {
        if (id != command.Id)
            return BadRequest("Route ID does not match payload ID.");

        if (!await _caseAuth.HasCaseAccessAsync(id, CaseAccessLevel.ReadWrite, ct))
        {
            return Forbid();
        }

        await Sender.Send(command, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/assign")]
    public async Task<IActionResult> Assign(Guid id, [FromBody] AssignCaseCommand command, CancellationToken ct)
    {
        if (id != command.Id)
            return BadRequest("Route ID does not match payload ID.");

        if (!await _caseAuth.HasCaseAccessAsync(id, CaseAccessLevel.Admin, ct))
        {
            return Forbid();
        }

        await Sender.Send(command, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/priority")]
    public async Task<IActionResult> UpdatePriority(Guid id, [FromBody] UpdateCasePriorityCommand command, CancellationToken ct)
    {
        if (id != command.Id)
            return BadRequest("Route ID does not match payload ID.");

        if (!await _caseAuth.HasCaseAccessAsync(id, CaseAccessLevel.ReadWrite, ct))
        {
            return Forbid();
        }

        await Sender.Send(command, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/timeline")]
    public async Task<ActionResult<IReadOnlyList<CaseTimelineItemDto>>> GetTimeline(Guid id, CancellationToken ct)
    {
        if (!await _caseAuth.HasCaseAccessAsync(id, CaseAccessLevel.Read, ct))
        {
            return Forbid();
        }

        return Ok(await Sender.Send(new GetCaseTimelineQuery(id), ct));
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<CaseDashboardMetricsDto>> GetDashboardMetrics(CancellationToken ct)
    {
        return Ok(await Sender.Send(new GetCaseDashboardMetricsQuery(), ct));
    }
}
