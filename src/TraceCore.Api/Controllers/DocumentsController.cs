using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TraceCore.Application.Common.Interfaces;
using TraceCore.Application.Documents;
using TraceCore.Domain.Enums;
using TraceCore.Infrastructure.Security;

namespace TraceCore.Api.Controllers;

[Authorize]
public class DocumentsController : ApiControllerBase
{
    private readonly IFileStorage _storage;
    private readonly IApplicationDbContext _context;
    private readonly ICaseAuthorizationService _caseAuth;

    public DocumentsController(IFileStorage storage, IApplicationDbContext context, ICaseAuthorizationService caseAuth)
    {
        _storage = storage;
        _context = context;
        _caseAuth = caseAuth;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DocumentDto>>> GetDocuments([FromQuery] Guid caseId, CancellationToken ct)
    {
        if (!await _caseAuth.HasCaseAccessAsync(caseId, CaseAccessLevel.Read, ct))
        {
            return Forbid();
        }

        return Ok(await Sender.Send(new GetDocumentsByCaseIdQuery(caseId), ct));
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<Guid>> UploadDocument(
        [FromForm] IFormFile file,
        [FromForm] Guid caseId,
        [FromForm] string name,
        [FromForm] string documentType,
        [FromForm] string description,
        [FromForm] ConfidentialityLevel confidentialityLevel,
        CancellationToken ct)
    {
        if (!await _caseAuth.HasCaseAccessAsync(caseId, CaseAccessLevel.ReadWrite, ct))
        {
            return Forbid();
        }

        if (file == null || file.Length == 0)
            return BadRequest("File cannot be empty.");

        using var stream = file.OpenReadStream();
        string hash = await _storage.ComputeSha256Async(stream, ct);
        string storagePath = await _storage.SaveFileAsync(stream, file.FileName, file.ContentType, ct);

        var command = new CreateDocumentCommand(
            caseId,
            name,
            documentType,
            description,
            confidentialityLevel,
            file.FileName,
            file.ContentType,
            file.Length,
            storagePath,
            hash);

        var docId = await Sender.Send(command, ct);
        return Ok(docId);
    }

    [HttpPost("{id:guid}/versions")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<DocumentVersionDto>> UploadNewVersion(
        Guid id,
        [FromForm] IFormFile file,
        [FromForm] string changeSummary,
        CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest("File cannot be empty.");

        using var stream = file.OpenReadStream();
        string hash = await _storage.ComputeSha256Async(stream, ct);
        string storagePath = await _storage.SaveFileAsync(stream, file.FileName, file.ContentType, ct);

        var command = new AddDocumentVersionCommand(
            id,
            file.FileName,
            file.ContentType,
            file.Length,
            storagePath,
            hash,
            changeSummary);

        var version = await Sender.Send(command, ct);
        return Ok(version);
    }

    [HttpGet("{id:guid}/versions/{versionNumber:int}/download")]
    public async Task<IActionResult> DownloadVersion(Guid id, int versionNumber, CancellationToken ct)
    {
        var version = await _context.DocumentVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.DocumentId == id && v.VersionNumber == versionNumber, ct);

        if (version == null)
            return NotFound("Requested document version was not found.");

        var stream = await _storage.GetFileStreamAsync(version.StoragePath, ct);
        return File(stream, version.ContentType, version.FileName);
    }
}
