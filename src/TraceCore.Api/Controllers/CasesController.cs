using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TraceCore.Api.Common;
using TraceCore.Api.Services;
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

    public CasesController(ISender sender, ICaseAuthorizationService caseAuth)
        : base(sender)
    {
        _caseAuth = caseAuth;
    }

    [HttpGet]
    [HasPermission(Permissions.CaseRead)]
    public async Task<ActionResult<PagedList<CaseDto>>> GetCases([FromQuery] GetCasesPagedQuery query, CancellationToken ct)
    {
        return Ok(await Sender.Send(query, ct));
    }

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.CaseRead)]
    public async Task<ActionResult<CaseDetailDto>> GetCaseById(Guid id, CancellationToken ct)
    {
        if (!await _caseAuth.HasCaseAccessAsync(id, CaseAccessLevel.Read, ct))
        {
            return Forbid();
        }

        return Ok(await Sender.Send(new GetCaseByIdQuery(id), ct));
    }

    [HttpPost]
    [HasPermission(Permissions.CaseCreate)]
    public async Task<ActionResult<CreateCaseResult>> CreateCase([FromBody] CreateCaseCommand command, CancellationToken ct)
    {
        var result = await Sender.Send(command, ct);
        return CreatedAtAction(nameof(GetCaseById), new { id = result.CaseId }, result);
    }

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.CaseUpdate)]
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
    [HasPermission(Permissions.CaseUpdate)]
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
    [HasPermission(Permissions.InvestigationAssign)]
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
    [HasPermission(Permissions.CaseUpdate)]
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
    [HasPermission(Permissions.CaseRead)]
    public async Task<ActionResult<IReadOnlyList<CaseTimelineItemDto>>> GetTimeline(Guid id, CancellationToken ct)
    {
        if (!await _caseAuth.HasCaseAccessAsync(id, CaseAccessLevel.Read, ct))
        {
            return Forbid();
        }

        return Ok(await Sender.Send(new GetCaseTimelineQuery(id), ct));
    }

    [HttpGet("dashboard")]
    [HasPermission(Permissions.ReportsRead)]
    public async Task<ActionResult<CaseDashboardMetricsDto>> GetDashboardMetrics(CancellationToken ct)
    {
        return Ok(await Sender.Send(new GetCaseDashboardMetricsQuery(), ct));
    }
}
