using System.Security.Cryptography;
using System.Text;
using TraceCore.Domain.Common;
using TraceCore.Domain.Common.Exceptions;
using TraceCore.Domain.Enums;
using TraceCore.Domain.Events;

namespace TraceCore.Domain.Entities.Evidence;

public class Evidence : BaseEntity, IAggregateRoot
{
    public Guid CaseId { get; private set; }
    public string EvidenceNumber { get; private set; } = default!;
    public EvidenceType Type { get; private set; }
    public string Description { get; private set; } = default!;
    public string Source { get; private set; } = default!;
    public DateTime CollectedAtUtc { get; private set; }
    public Guid CollectedByUserId { get; private set; }
    public EvidenceStatus Status { get; private set; }
    public ConfidentialityLevel ConfidentialityLevel { get; private set; }
    public string StorageLocation { get; private set; } = default!;
    public string Hash { get; private set; } = default!;
    public bool IsCritical { get; private set; }
    public byte[] RowVersion { get; set; } = [];

    private readonly List<EvidenceCustodyEvent> _custodyEvents = [];
    public IReadOnlyCollection<EvidenceCustodyEvent> CustodyEvents => _custodyEvents.AsReadOnly();

    protected Evidence() : base() { }

    public static Evidence Collect(
        Guid caseId,
        string evidenceNumber,
        EvidenceType type,
        string description,
        string source,
        Guid collectedByUserId,
        ConfidentialityLevel confidentiality,
        string storageLocation,
        string hash,
        bool isCritical,
        string notes = "")
    {
        if (caseId == Guid.Empty)
            throw new DomainException("Case ID is required for evidence.");
        if (string.IsNullOrWhiteSpace(evidenceNumber))
            throw new DomainException("Evidence number cannot be empty.");
        if (string.IsNullOrWhiteSpace(description))
            throw new DomainException("Evidence description cannot be empty.");
        if (string.IsNullOrWhiteSpace(hash))
            throw new DomainException("Cryptographic hash is required to guarantee evidence integrity.");

        var evidence = new Evidence
        {
            Id = Guid.NewGuid(),
            CaseId = caseId,
            EvidenceNumber = evidenceNumber.Trim().ToUpperInvariant(),
            Type = type,
            Description = description.Trim(),
            Source = source?.Trim() ?? "Unknown",
            CollectedAtUtc = DateTime.UtcNow,
            CollectedByUserId = collectedByUserId,
            Status = EvidenceStatus.InCustody,
            ConfidentialityLevel = confidentiality,
            StorageLocation = storageLocation?.Trim() ?? "Evidence Locker",
            Hash = hash.Trim().ToUpperInvariant(),
            IsCritical = isCritical,
            CreatedAtUtc = DateTime.UtcNow
        };

        // Create initial Genesis custody event
        var genesisEvent = EvidenceCustodyEvent.CreateInitial(
            evidence.Id,
            collectedByUserId,
            evidence.StorageLocation,
            evidence.Hash,
            string.IsNullOrWhiteSpace(notes) ? "Initial evidence acquisition & intake." : notes);

        evidence._custodyEvents.Add(genesisEvent);

        evidence.AddDomainEvent(new EvidenceAddedDomainEvent(
            evidence.Id,
            evidence.CaseId,
            evidence.EvidenceNumber,
            evidence.Type,
            evidence.IsCritical));

        return evidence;
    }

    public EvidenceCustodyEvent RecordTransfer(
        Guid fromUserId,
        Guid toUserId,
        CustodyAction action,
        string newLocation,
        string notes,
        string? verifiedHash = null)
    {
        if (Status == EvidenceStatus.Disposed)
            throw new DomainException("Cannot transfer custody of disposed evidence.");

        // Hash verification check if provided
        if (!string.IsNullOrWhiteSpace(verifiedHash))
        {
            string cleanVerified = verifiedHash.Trim().ToUpperInvariant();
            if (cleanVerified != Hash)
            {
                throw new DomainException($"Hash mismatch! Expected {Hash} but received {cleanVerified}. Evidence tampering detected.");
            }
        }

        var lastEvent = _custodyEvents.OrderByDescending(e => e.TimestampUtc).FirstOrDefault()
            ?? throw new DomainException("Corrupt custody chain: missing genesis custody event.");

        var newEvent = EvidenceCustodyEvent.CreateChained(
            Id,
            action,
            fromUserId,
            toUserId,
            newLocation,
            notes,
            lastEvent.CurrentHash);

        _custodyEvents.Add(newEvent);
        StorageLocation = newLocation?.Trim() ?? StorageLocation;
        UpdatedAtUtc = DateTime.UtcNow;

        Status = action switch
        {
            CustodyAction.CheckedOutForAnalysis => EvidenceStatus.InAnalysis,
            CustodyAction.CheckedIn => EvidenceStatus.InCustody,
            CustodyAction.CourtPresented => EvidenceStatus.CourtExhibited,
            CustodyAction.Archived => EvidenceStatus.Archived,
            CustodyAction.Destroyed => EvidenceStatus.Disposed,
            _ => EvidenceStatus.InCustody
        };

        AddDomainEvent(new EvidenceCustodyTransferredDomainEvent(
            Id,
            CaseId,
            fromUserId,
            toUserId,
            action,
            StorageLocation,
            newEvent.CurrentHash));

        return newEvent;
    }

    public void Archive(Guid userId, string reason)
    {
        RecordTransfer(userId, userId, CustodyAction.Archived, "Long-term Evidence Archive", reason);
    }
}

public class EvidenceCustodyEvent : BaseEntity
{
    public Guid EvidenceId { get; private set; }
    public CustodyAction Action { get; private set; }
    public Guid FromUserId { get; private set; }
    public Guid ToUserId { get; private set; }
    public DateTime TimestampUtc { get; private set; }
    public string Location { get; private set; } = default!;
    public string Notes { get; private set; } = string.Empty;
    public string PreviousHash { get; private set; } = default!;
    public string CurrentHash { get; private set; } = default!;

    protected EvidenceCustodyEvent() : base() { }

    public static EvidenceCustodyEvent CreateInitial(
        Guid evidenceId,
        Guid collectorUserId,
        string location,
        string evidenceHash,
        string notes)
    {
        var now = DateTime.UtcNow;
        var timestamp = new DateTime(now.Ticks - (now.Ticks % TimeSpan.TicksPerMillisecond), DateTimeKind.Utc);
        string previousHash = "0000000000000000000000000000000000000000000000000000000000000000";
        string currentHash = ComputeChainHash(evidenceId, CustodyAction.Collected, collectorUserId, collectorUserId, timestamp, location, previousHash, evidenceHash);

        return new EvidenceCustodyEvent
        {
            Id = Guid.NewGuid(),
            EvidenceId = evidenceId,
            Action = CustodyAction.Collected,
            FromUserId = collectorUserId,
            ToUserId = collectorUserId,
            TimestampUtc = timestamp,
            Location = location,
            Notes = notes,
            PreviousHash = previousHash,
            CurrentHash = currentHash,
            CreatedAtUtc = timestamp
        };
    }

    public static EvidenceCustodyEvent CreateChained(
        Guid evidenceId,
        CustodyAction action,
        Guid fromUserId,
        Guid toUserId,
        string location,
        string notes,
        string previousHash)
    {
        var now = DateTime.UtcNow;
        var timestamp = new DateTime(now.Ticks - (now.Ticks % TimeSpan.TicksPerMillisecond), DateTimeKind.Utc);
        string currentHash = ComputeChainHash(evidenceId, action, fromUserId, toUserId, timestamp, location, previousHash);

        return new EvidenceCustodyEvent
        {
            Id = Guid.NewGuid(),
            EvidenceId = evidenceId,
            Action = action,
            FromUserId = fromUserId,
            ToUserId = toUserId,
            TimestampUtc = timestamp,
            Location = location,
            Notes = notes,
            PreviousHash = previousHash,
            CurrentHash = currentHash,
            CreatedAtUtc = timestamp
        };
    }

    public static string ComputeChainHash(
        Guid evidenceId,
        CustodyAction action,
        Guid fromUserId,
        Guid toUserId,
        DateTime timestampUtc,
        string location,
        string previousHash,
        string? initialPayload = null)
    {
        var utc = DateTime.SpecifyKind(timestampUtc, DateTimeKind.Utc);
        string raw = $"{evidenceId}|{action}|{fromUserId}|{toUserId}|{utc:yyyy-MM-ddTHH:mm:ss.fffZ}|{location}|{previousHash}|{initialPayload ?? string.Empty}";
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes);
    }
}
