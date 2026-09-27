using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TraceCore.Api.Common;
using TraceCore.Api.Services;
using TraceCore.Application.Common.Interfaces;
using TraceCore.Application.Evidence;
using TraceCore.Domain.Enums;
using TraceCore.Infrastructure.Security;

namespace TraceCore.Api.Controllers;

[Authorize]
public class EvidenceController : ApiControllerBase
{
    private readonly ICaseAuthorizationService _caseAuth;
    private readonly IApplicationDbContext _context;

    public EvidenceController(ISender sender, ICaseAuthorizationService caseAuth, IApplicationDbContext context)
        : base(sender)
    {
        _caseAuth = caseAuth;
        _context = context;
    }

    [HttpGet]
    [HasPermission(Permissions.EvidenceRead)]
    public async Task<ActionResult<IReadOnlyList<EvidenceDto>>> GetEvidenceByCase([FromQuery] Guid caseId, CancellationToken ct)
    {
        if (!await _caseAuth.HasCaseAccessAsync(caseId, CaseAccessLevel.Read, ct))
        {
            return Forbid();
        }

        return Ok(await Sender.Send(new GetEvidenceByCaseIdQuery(caseId), ct));
    }

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.EvidenceRead)]
    public async Task<ActionResult<EvidenceDetailDto>> GetById(Guid id, CancellationToken ct)
    {
        var caseId = await _context.Evidence
            .AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => (Guid?)e.CaseId)
            .FirstOrDefaultAsync(ct);
        if (caseId is null)
            return NotFound();

        if (!await _caseAuth.HasCaseAccessAsync(caseId.Value, CaseAccessLevel.Read, ct))
        {
            return Forbid();
        }

        return Ok(await Sender.Send(new GetEvidenceByIdQuery(id), ct));
    }

    [HttpPost]
    [HasPermission(Permissions.EvidenceCreate)]
    public async Task<ActionResult<Guid>> Create([FromBody] CreateEvidenceCommand command, CancellationToken ct)
    {
        if (!await _caseAuth.HasCaseAccessAsync(command.CaseId, CaseAccessLevel.ReadWrite, ct))
        {
            return Forbid();
        }

        var id = await Sender.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [HttpPost("{id:guid}/transfer")]
    [HasPermission(Permissions.EvidenceTransfer)]
    public async Task<IActionResult> Transfer(Guid id, [FromBody] TransferEvidenceCustodyCommand command, CancellationToken ct)
    {
        if (id != command.EvidenceId)
            return BadRequest("Route ID does not match command ID.");

        var caseId = await _context.Evidence
            .AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => (Guid?)e.CaseId)
            .FirstOrDefaultAsync(ct);
        if (caseId is null)
            return NotFound();

        if (!await _caseAuth.HasCaseAccessAsync(caseId.Value, CaseAccessLevel.ReadWrite, ct))
            return Forbid();

        await Sender.Send(command, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/verify")]
    [HasPermission(Permissions.EvidenceRead)]
    public async Task<ActionResult<EvidenceVerificationDto>> VerifyChain(Guid id, CancellationToken ct)
    {
        var caseId = await _context.Evidence
            .AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => (Guid?)e.CaseId)
            .FirstOrDefaultAsync(ct);
        if (caseId is null)
            return NotFound();

        if (!await _caseAuth.HasCaseAccessAsync(caseId.Value, CaseAccessLevel.Read, ct))
            return Forbid();

        return Ok(await Sender.Send(new VerifyEvidenceChainQuery(id), ct));
    }
}
