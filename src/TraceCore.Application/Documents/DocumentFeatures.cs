using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TraceCore.Application.Common.Exceptions;
using TraceCore.Application.Common.Interfaces;
using TraceCore.Domain.Entities.Documents;
using TraceCore.Domain.Enums;

namespace TraceCore.Application.Documents;

public sealed record DocumentVersionDto(
    Guid Id,
    Guid DocumentId,
    int VersionNumber,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    string StoragePath,
    string Sha256Hash,
    string ChangeSummary,
    Guid UploadedByUserId,
    DateTime UploadedAtUtc);

public sealed record DocumentDto(
    Guid Id,
    Guid CaseId,
    string Name,
    string DocumentType,
    string Description,
    ConfidentialityLevel ConfidentialityLevel,
    Guid CreatedByUserId,
    DateTime CreatedAtUtc,
    int TotalVersions,
    DocumentVersionDto? LatestVersion);

// 1. Create Document
public sealed record CreateDocumentCommand(
    Guid CaseId,
    string Name,
    string DocumentType,
    string Description,
    ConfidentialityLevel ConfidentialityLevel,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    string StoragePath,
    string Sha256Hash,
    string ChangeSummary = "Initial Document Upload") : IRequest<Guid>;

public class CreateDocumentCommandValidator : AbstractValidator<CreateDocumentCommand>
{
    public CreateDocumentCommandValidator()
    {
        RuleFor(v => v.CaseId).NotEmpty();
        RuleFor(v => v.Name).NotEmpty().MaximumLength(200);
        RuleFor(v => v.FileName).NotEmpty();
        RuleFor(v => v.Sha256Hash).NotEmpty().Length(64);
    }
}

public class CreateDocumentCommandHandler : IRequestHandler<CreateDocumentCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreateDocumentCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Guid> Handle(CreateDocumentCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? Guid.Parse("11111111-1111-1111-1111-111111111111");

        var doc = Document.Create(
            request.CaseId,
            request.Name,
            request.DocumentType,
            request.Description,
            request.ConfidentialityLevel,
            userId,
            request.FileName,
            request.ContentType,
            request.FileSizeBytes,
            request.StoragePath,
            request.Sha256Hash,
            request.ChangeSummary);

        _context.Add(doc);
        await _context.SaveChangesAsync(cancellationToken);

        return doc.Id;
    }
}

// 2. Add Document Version
public sealed record AddDocumentVersionCommand(
    Guid DocumentId,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    string StoragePath,
    string Sha256Hash,
    string ChangeSummary) : IRequest<DocumentVersionDto>;

public class AddDocumentVersionCommandValidator : AbstractValidator<AddDocumentVersionCommand>
{
    public AddDocumentVersionCommandValidator()
    {
        RuleFor(v => v.DocumentId).NotEmpty();
        RuleFor(v => v.FileName).NotEmpty();
        RuleFor(v => v.Sha256Hash).NotEmpty().Length(64);
        RuleFor(v => v.ChangeSummary).NotEmpty();
    }
}

public class AddDocumentVersionCommandHandler : IRequestHandler<AddDocumentVersionCommand, DocumentVersionDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public AddDocumentVersionCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<DocumentVersionDto> Handle(AddDocumentVersionCommand request, CancellationToken cancellationToken)
    {
        var doc = await _context.Documents
            .Include(d => d.Versions)
            .FirstOrDefaultAsync(d => d.Id == request.DocumentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Document), request.DocumentId);

        var userId = _currentUser.UserId ?? Guid.Parse("11111111-1111-1111-1111-111111111111");

        var version = doc.AddVersion(
            request.FileName,
            request.ContentType,
            request.FileSizeBytes,
            request.StoragePath,
            request.Sha256Hash,
            userId,
            request.ChangeSummary);

        _context.Add(version);

        await _context.SaveChangesAsync(cancellationToken);

        return new DocumentVersionDto(
            version.Id,
            version.DocumentId,
            version.VersionNumber,
            version.FileName,
            version.ContentType,
            version.FileSizeBytes,
            version.StoragePath,
            version.Sha256Hash,
            version.ChangeSummary,
            version.UploadedByUserId,
            version.UploadedAtUtc);
    }
}

// 3. Get Documents by Case Id
public sealed record GetDocumentsByCaseIdQuery(Guid CaseId) : IRequest<IReadOnlyList<DocumentDto>>;

public class GetDocumentsByCaseIdQueryHandler : IRequestHandler<GetDocumentsByCaseIdQuery, IReadOnlyList<DocumentDto>>
{
    private readonly IApplicationDbContext _context;

    public GetDocumentsByCaseIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<DocumentDto>> Handle(GetDocumentsByCaseIdQuery request, CancellationToken cancellationToken)
    {
        var docs = await _context.Documents
            .Include(d => d.Versions)
            .AsNoTracking()
            .Where(d => d.CaseId == request.CaseId)
            .OrderByDescending(d => d.CreatedAtUtc)
            .Take(200)
            .ToListAsync(cancellationToken);

        return docs.Select(d =>
        {
            var latest = d.LatestVersion;
            var latestDto = latest == null ? null : new DocumentVersionDto(
                latest.Id,
                latest.DocumentId,
                latest.VersionNumber,
                latest.FileName,
                latest.ContentType,
                latest.FileSizeBytes,
                latest.StoragePath,
                latest.Sha256Hash,
                latest.ChangeSummary,
                latest.UploadedByUserId,
                latest.UploadedAtUtc);

            return new DocumentDto(
                d.Id,
                d.CaseId,
                d.Name,
                d.DocumentType,
                d.Description,
                d.ConfidentialityLevel,
                d.CreatedByUserId,
                d.CreatedAtUtc,
                d.Versions.Count,
                latestDto);
        }).ToList();
    }
}
