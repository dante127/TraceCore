using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TraceCore.Application.Evidence;
using TraceCore.Domain.Enums;
using TraceCore.Infrastructure.Security;

namespace TraceCore.Api.Controllers;

[Authorize]
public class EvidenceController : ApiControllerBase
{
    private readonly ICaseAuthorizationService _caseAuth;

    public EvidenceController(ICaseAuthorizationService caseAuth)
    {
        _caseAuth = caseAuth;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EvidenceDto>>> GetEvidenceByCase([FromQuery] Guid caseId, CancellationToken ct)
    {
        if (!await _caseAuth.HasCaseAccessAsync(caseId, CaseAccessLevel.Read, ct))
        {
            return Forbid();
        }

        return Ok(await Sender.Send(new GetEvidenceByCaseIdQuery(caseId), ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EvidenceDetailDto>> GetById(Guid id, CancellationToken ct)
    {
        var evidence = await Sender.Send(new GetEvidenceByIdQuery(id), ct);
        if (!await _caseAuth.HasCaseAccessAsync(evidence.CaseId, CaseAccessLevel.Read, ct))
        {
            return Forbid();
        }

        return Ok(evidence);
    }

    [HttpPost]
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
    public async Task<IActionResult> Transfer(Guid id, [FromBody] TransferEvidenceCustodyCommand command, CancellationToken ct)
    {
        if (id != command.EvidenceId)
            return BadRequest("Route ID does not match command ID.");

        await Sender.Send(command, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/verify")]
    public async Task<ActionResult<EvidenceVerificationDto>> VerifyChain(Guid id, CancellationToken ct)
    {
        return Ok(await Sender.Send(new VerifyEvidenceChainQuery(id), ct));
    }
}
