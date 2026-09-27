using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TraceCore.Api.Common;
using TraceCore.Api.Services;
using TraceCore.Application.Common.Interfaces;
using TraceCore.Application.Investigations;
using TraceCore.Domain.Enums;
using TraceCore.Infrastructure.Security;

namespace TraceCore.Api.Controllers;

[Authorize]
public class InvestigationsController : ApiControllerBase
{
    private readonly ICaseAuthorizationService _caseAuth;
    private readonly IApplicationDbContext _context;

    public InvestigationsController(ISender sender, ICaseAuthorizationService caseAuth, IApplicationDbContext context)
        : base(sender)
    {
        _caseAuth = caseAuth;
        _context = context;
    }

    [HttpGet]
    [HasPermission(Permissions.InvestigationRead)]
    public async Task<ActionResult<IReadOnlyList<InvestigationDto>>> GetInvestigations([FromQuery] Guid caseId, CancellationToken ct)
    {
        if (!await _caseAuth.HasCaseAccessAsync(caseId, CaseAccessLevel.Read, ct))
        {
            return Forbid();
        }

        return Ok(await Sender.Send(new GetInvestigationsByCaseIdQuery(caseId), ct));
    }

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.InvestigationRead)]
    public async Task<ActionResult<InvestigationDetailDto>> GetById(Guid id, CancellationToken ct)
    {
        var caseId = await _context.Investigations
            .AsNoTracking()
            .Where(i => i.Id == id)
            .Select(i => (Guid?)i.CaseId)
            .FirstOrDefaultAsync(ct);
        if (caseId is null)
            return NotFound();

        if (!await _caseAuth.HasCaseAccessAsync(caseId.Value, CaseAccessLevel.Read, ct))
        {
            return Forbid();
        }

        return Ok(await Sender.Send(new GetInvestigationByIdQuery(id), ct));
    }

    [HttpPost]
    [HasPermission(Permissions.InvestigationCreate)]
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
    [HasPermission(Permissions.InvestigationCreate)]
    public async Task<ActionResult<Guid>> AddActivity(Guid id, [FromBody] AddInvestigationActivityCommand command, CancellationToken ct)
    {
        if (id != command.InvestigationId)
            return BadRequest("Route ID does not match command ID.");

        var caseId = await _context.Investigations
            .AsNoTracking()
            .Where(i => i.Id == id)
            .Select(i => (Guid?)i.CaseId)
            .FirstOrDefaultAsync(ct);
        if (caseId is null)
            return NotFound();

        if (!await _caseAuth.HasCaseAccessAsync(caseId.Value, CaseAccessLevel.ReadWrite, ct))
        {
            return Forbid();
        }

        var activityId = await Sender.Send(command, ct);
        return StatusCode(StatusCodes.Status201Created, activityId);
    }

    [HttpPut("{id:guid}/findings")]
    [HasPermission(Permissions.InvestigationReview)]
    public async Task<IActionResult> UpdateFindings(Guid id, [FromBody] UpdateInvestigationFindingsCommand command, CancellationToken ct)
    {
        if (id != command.InvestigationId)
            return BadRequest("Route ID does not match command ID.");

        var caseId = await _context.Investigations
            .AsNoTracking()
            .Where(i => i.Id == id)
            .Select(i => (Guid?)i.CaseId)
            .FirstOrDefaultAsync(ct);
        if (caseId is null)
            return NotFound();

        if (!await _caseAuth.HasCaseAccessAsync(caseId.Value, CaseAccessLevel.ReadWrite, ct))
        {
            return Forbid();
        }

        await Sender.Send(command, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/status")]
    [HasPermission(Permissions.InvestigationReview)]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateInvestigationStatusCommand command, CancellationToken ct)
    {
        if (id != command.InvestigationId)
            return BadRequest("Route ID does not match command ID.");

        var caseId = await _context.Investigations
            .AsNoTracking()
            .Where(i => i.Id == id)
            .Select(i => (Guid?)i.CaseId)
            .FirstOrDefaultAsync(ct);
        if (caseId is null)
            return NotFound();

        if (!await _caseAuth.HasCaseAccessAsync(caseId.Value, CaseAccessLevel.ReadWrite, ct))
        {
            return Forbid();
        }

        await Sender.Send(command, ct);
        return NoContent();
    }
}
