using TraceCore.Domain.Common;
using TraceCore.Domain.Common.Exceptions;
using TraceCore.Domain.Enums;

namespace TraceCore.Domain.Entities.Documents;

public class Document : BaseEntity, IAggregateRoot
{
    public Guid CaseId { get; private set; }
    public string Name { get; private set; } = default!;
    public string DocumentType { get; private set; } = default!;
    public string Description { get; private set; } = string.Empty;
    public ConfidentialityLevel ConfidentialityLevel { get; private set; }
    public Guid CreatedByUserId { get; private set; }

    private readonly List<DocumentVersion> _versions = [];
    public IReadOnlyCollection<DocumentVersion> Versions => _versions.AsReadOnly();

    public DocumentVersion? LatestVersion => _versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();

    protected Document() : base() { }

    public static Document Create(
        Guid caseId,
        string name,
        string documentType,
        string description,
        ConfidentialityLevel confidentiality,
        Guid createdByUserId,
        string fileName,
        string contentType,
        long fileSizeBytes,
        string storagePath,
        string sha256Hash,
        string changeSummary = "Initial version")
    {
        if (caseId == Guid.Empty)
            throw new DomainException("Case ID is required.");
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Document name cannot be empty.");

        var document = new Document
        {
            Id = Guid.NewGuid(),
            CaseId = caseId,
            Name = name.Trim(),
            DocumentType = string.IsNullOrWhiteSpace(documentType) ? "General" : documentType.Trim(),
            Description = description?.Trim() ?? string.Empty,
            ConfidentialityLevel = confidentiality,
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = DateTime.UtcNow
        };

        var initialVersion = new DocumentVersion(
            document.Id,
            1,
            fileName,
            contentType,
            fileSizeBytes,
            storagePath,
            sha256Hash,
            createdByUserId,
            changeSummary);

        document._versions.Add(initialVersion);
        return document;
    }

    public DocumentVersion AddVersion(
        string fileName,
        string contentType,
        long fileSizeBytes,
        string storagePath,
        string sha256Hash,
        Guid uploadedByUserId,
        string changeSummary)
    {
        int nextVersionNumber = (_versions.MaxBy(v => v.VersionNumber)?.VersionNumber ?? 0) + 1;

        var newVersion = new DocumentVersion(
            Id,
            nextVersionNumber,
            fileName,
            contentType,
            fileSizeBytes,
            storagePath,
            sha256Hash,
            uploadedByUserId,
            changeSummary);

        _versions.Add(newVersion);
        UpdatedAtUtc = DateTime.UtcNow;
        return newVersion;
    }
}

public class DocumentVersion : BaseEntity
{
    public Guid DocumentId { get; private set; }
    public int VersionNumber { get; private set; }
    public string FileName { get; private set; } = default!;
    public string ContentType { get; private set; } = default!;
    public long FileSizeBytes { get; private set; }
    public string StoragePath { get; private set; } = default!;
    public string Sha256Hash { get; private set; } = default!;
    public string ChangeSummary { get; private set; } = string.Empty;
    public Guid UploadedByUserId { get; private set; }
    public DateTime UploadedAtUtc { get; private set; } = DateTime.UtcNow;

    protected DocumentVersion() : base() { }

    public DocumentVersion(
        Guid documentId,
        int versionNumber,
        string fileName,
        string contentType,
        long fileSizeBytes,
        string storagePath,
        string sha256Hash,
        Guid uploadedByUserId,
        string changeSummary) : base()
    {
        if (documentId == Guid.Empty)
            throw new DomainException("Document ID is required.");
        if (versionNumber <= 0)
            throw new DomainException("Version number must be positive.");
        if (string.IsNullOrWhiteSpace(fileName))
            throw new DomainException("File name is required.");
        if (fileSizeBytes <= 0)
            throw new DomainException("File size must be positive.");
        if (string.IsNullOrWhiteSpace(storagePath))
            throw new DomainException("Storage path is required.");
        if (string.IsNullOrWhiteSpace(sha256Hash))
            throw new DomainException("SHA-256 hash is required to guarantee file integrity.");
        if (uploadedByUserId == Guid.Empty)
            throw new DomainException("Uploading user is required.");

        DocumentId = documentId;
        VersionNumber = versionNumber;
        FileName = fileName.Trim();
        ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType.Trim();
        FileSizeBytes = fileSizeBytes;
        StoragePath = storagePath.Trim();
        Sha256Hash = sha256Hash.Trim().ToUpperInvariant();
        UploadedByUserId = uploadedByUserId;
        ChangeSummary = changeSummary?.Trim() ?? string.Empty;
        UploadedAtUtc = DateTime.UtcNow;
    }
}
