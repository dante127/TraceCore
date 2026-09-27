using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TraceCore.Application.Common.Exceptions;
using TraceCore.Application.Common.Interfaces;
using TraceCore.Domain.Entities.Evidence;
using TraceCore.Domain.Enums;
using EvidenceEntity = TraceCore.Domain.Entities.Evidence.Evidence;

namespace TraceCore.Application.Evidence;

public sealed record EvidenceDto(
    Guid Id,
    Guid CaseId,
    string EvidenceNumber,
    EvidenceType Type,
    string Description,
    string Source,
    DateTime CollectedAtUtc,
    Guid CollectedByUserId,
    EvidenceStatus Status,
    ConfidentialityLevel ConfidentialityLevel,
    string StorageLocation,
    string Hash,
    bool IsCritical,
    DateTime CreatedAtUtc,
    string RowVersion);

public sealed record EvidenceCustodyEventDto(
    Guid Id,
    Guid EvidenceId,
    CustodyAction Action,
    Guid FromUserId,
    Guid ToUserId,
    DateTime TimestampUtc,
    string Location,
    string Notes,
    string PreviousHash,
    string CurrentHash);

public sealed record EvidenceDetailDto(
    Guid Id,
    Guid CaseId,
    string EvidenceNumber,
    EvidenceType Type,
    string Description,
    string Source,
    DateTime CollectedAtUtc,
    Guid CollectedByUserId,
    EvidenceStatus Status,
    ConfidentialityLevel ConfidentialityLevel,
    string StorageLocation,
    string Hash,
    bool IsCritical,
    DateTime CreatedAtUtc,
    string RowVersion,
    IReadOnlyList<EvidenceCustodyEventDto> CustodyHistory);

public sealed record EvidenceVerificationDto(
    Guid EvidenceId,
    string EvidenceNumber,
    bool IsChainValid,
    int TotalEventsVerified,
    string? BrokenAtEventId,
    string Message);

// 1. Create Evidence
public sealed record CreateEvidenceCommand(
    Guid CaseId,
    EvidenceType Type,
    string Description,
    string Source,
    ConfidentialityLevel ConfidentialityLevel,
    string StorageLocation,
    string Hash,
    bool IsCritical,
    string InitialNotes = "") : IRequest<Guid>;

public class CreateEvidenceCommandValidator : AbstractValidator<CreateEvidenceCommand>
{
    public CreateEvidenceCommandValidator()
    {
        RuleFor(v => v.CaseId).NotEmpty();
        RuleFor(v => v.Description).NotEmpty().MaximumLength(500);
        RuleFor(v => v.Type).IsInEnum();
        RuleFor(v => v.Hash).NotEmpty().Length(64); // SHA-256 hex string length
    }
}

public class CreateEvidenceCommandHandler : IRequestHandler<CreateEvidenceCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public CreateEvidenceCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IDateTimeProvider dateTime)
    {
        _context = context;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public async Task<Guid> Handle(CreateEvidenceCommand request, CancellationToken cancellationToken)
    {
        var existingCount = await _context.Evidence
            .CountAsync(e => e.CaseId == request.CaseId, cancellationToken);

        var currentYear = _dateTime.UtcNow.Year;
        string evidenceNumber = $"EVD-{currentYear}-{(existingCount + 1):D4}";

        var collectorId = _currentUser.UserId ?? Guid.Parse("11111111-1111-1111-1111-111111111111");

        var evidence = Domain.Entities.Evidence.Evidence.Collect(
            request.CaseId,
            evidenceNumber,
            request.Type,
            request.Description,
            request.Source,
            collectorId,
            request.ConfidentialityLevel,
            request.StorageLocation,
            request.Hash,
            request.IsCritical,
            request.InitialNotes);

        _context.Add(evidence);
        await _context.SaveChangesAsync(cancellationToken);

        return evidence.Id;
    }
}

// 2. Transfer Evidence Custody
public sealed record TransferEvidenceCustodyCommand(
    Guid EvidenceId,
    Guid ToUserId,
    CustodyAction Action,
    string NewLocation,
    string Notes,
    string? VerifiedHash = null,
    string? RowVersion = null) : IRequest<Unit>;

public class TransferEvidenceCustodyCommandValidator : AbstractValidator<TransferEvidenceCustodyCommand>
{
    public TransferEvidenceCustodyCommandValidator()
    {
        RuleFor(v => v.EvidenceId).NotEmpty();
        RuleFor(v => v.ToUserId).NotEmpty();
        RuleFor(v => v.Action).IsInEnum();
        RuleFor(v => v.NewLocation).NotEmpty().MaximumLength(255);
        RuleFor(v => v.Notes).NotEmpty().MaximumLength(1000);
    }
}

public class TransferEvidenceCustodyCommandHandler : IRequestHandler<TransferEvidenceCustodyCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public TransferEvidenceCustodyCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(TransferEvidenceCustodyCommand request, CancellationToken cancellationToken)
    {
        var evidence = await _context.Evidence
            .Include(e => e.CustodyEvents)
            .FirstOrDefaultAsync(e => e.Id == request.EvidenceId, cancellationToken)
            ?? throw new NotFoundException(nameof(Evidence), request.EvidenceId);

        if (!string.IsNullOrEmpty(request.RowVersion))
        {
            var requestVersion = Convert.FromBase64String(request.RowVersion);
            if (!evidence.RowVersion.SequenceEqual(requestVersion))
            {
                throw new ConcurrencyException("The evidence record was modified concurrently. Please reload.");
            }
        }

        var fromUserId = _currentUser.UserId ?? Guid.Parse("11111111-1111-1111-1111-111111111111");

        var custodyEvent = evidence.RecordTransfer(
            fromUserId,
            request.ToUserId,
            request.Action,
            request.NewLocation,
            request.Notes,
            request.VerifiedHash);

        _context.Add(custodyEvent);

        await _context.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

// 3. Get Evidence By Id
public sealed record GetEvidenceByIdQuery(Guid Id) : IRequest<EvidenceDetailDto>;

public class GetEvidenceByIdQueryHandler : IRequestHandler<GetEvidenceByIdQuery, EvidenceDetailDto>
{
    private readonly IApplicationDbContext _context;

    public GetEvidenceByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<EvidenceDetailDto> Handle(GetEvidenceByIdQuery request, CancellationToken cancellationToken)
    {
        var evidence = await _context.Evidence
            .Include(e => e.CustodyEvents)
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Evidence), request.Id);

        var custodyDtos = evidence.CustodyEvents
            .OrderBy(c => c.TimestampUtc)
            .Select(c => new EvidenceCustodyEventDto(
                c.Id,
                c.EvidenceId,
                c.Action,
                c.FromUserId,
                c.ToUserId,
                c.TimestampUtc,
                c.Location,
                c.Notes,
                c.PreviousHash,
                c.CurrentHash))
            .ToList();

        return new EvidenceDetailDto(
            evidence.Id,
            evidence.CaseId,
            evidence.EvidenceNumber,
            evidence.Type,
            evidence.Description,
            evidence.Source,
            evidence.CollectedAtUtc,
            evidence.CollectedByUserId,
            evidence.Status,
            evidence.ConfidentialityLevel,
            evidence.StorageLocation,
            evidence.Hash,
            evidence.IsCritical,
            evidence.CreatedAtUtc,
            Convert.ToBase64String(evidence.RowVersion),
            custodyDtos);
    }
}

// 4. Get Evidence By Case Id
public sealed record GetEvidenceByCaseIdQuery(Guid CaseId) : IRequest<IReadOnlyList<EvidenceDto>>;

public class GetEvidenceByCaseIdQueryHandler : IRequestHandler<GetEvidenceByCaseIdQuery, IReadOnlyList<EvidenceDto>>
{
    private readonly IApplicationDbContext _context;

    public GetEvidenceByCaseIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<EvidenceDto>> Handle(GetEvidenceByCaseIdQuery request, CancellationToken cancellationToken)
    {
        return await _context.Evidence
            .AsNoTracking()
            .Where(e => e.CaseId == request.CaseId)
            .OrderByDescending(e => e.CreatedAtUtc)
            .Select(e => new EvidenceDto(
                e.Id,
                e.CaseId,
                e.EvidenceNumber,
                e.Type,
                e.Description,
                e.Source,
                e.CollectedAtUtc,
                e.CollectedByUserId,
                e.Status,
                e.ConfidentialityLevel,
                e.StorageLocation,
                e.Hash,
                e.IsCritical,
                e.CreatedAtUtc,
                Convert.ToBase64String(e.RowVersion)))
            .ToListAsync(cancellationToken);
    }
}

// 5. Verify Evidence Chain Integrity
public sealed record VerifyEvidenceChainQuery(Guid EvidenceId) : IRequest<EvidenceVerificationDto>;

public class VerifyEvidenceChainQueryHandler : IRequestHandler<VerifyEvidenceChainQuery, EvidenceVerificationDto>
{
    private readonly IApplicationDbContext _context;

    public VerifyEvidenceChainQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<EvidenceVerificationDto> Handle(VerifyEvidenceChainQuery request, CancellationToken cancellationToken)
    {
        var evidence = await _context.Evidence
            .Include(e => e.CustodyEvents)
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == request.EvidenceId, cancellationToken)
            ?? throw new NotFoundException(nameof(Evidence), request.EvidenceId);

        var events = evidence.CustodyEvents.OrderBy(e => e.TimestampUtc).ToList();
        if (events.Count == 0)
        {
            return new EvidenceVerificationDto(evidence.Id, evidence.EvidenceNumber, false, 0, null, "No custody events found.");
        }

        string expectedPreviousHash = "0000000000000000000000000000000000000000000000000000000000000000";

        for (int i = 0; i < events.Count; i++)
        {
            var evt = events[i];

            if (evt.PreviousHash != expectedPreviousHash)
            {
                return new EvidenceVerificationDto(
                    evidence.Id,
                    evidence.EvidenceNumber,
                    false,
                    i,
                    evt.Id.ToString(),
                    $"Custody chain broken at event #{i + 1} ({evt.Id}). Previous hash mismatch.");
            }

            // Recalculate hash
            string computedHash = i == 0
                ? EvidenceCustodyEvent.ComputeChainHash(evt.EvidenceId, evt.Action, evt.FromUserId, evt.ToUserId, evt.TimestampUtc, evt.Location, evt.PreviousHash, evidence.Hash)
                : EvidenceCustodyEvent.ComputeChainHash(evt.EvidenceId, evt.Action, evt.FromUserId, evt.ToUserId, evt.TimestampUtc, evt.Location, evt.PreviousHash);

            if (computedHash != evt.CurrentHash)
            {
                return new EvidenceVerificationDto(
                    evidence.Id,
                    evidence.EvidenceNumber,
                    false,
                    i,
                    evt.Id.ToString(),
                    $"Cryptographic tamper detected at event #{i + 1} ({evt.Id}). Computed {computedHash} != Recorded {evt.CurrentHash}.");
            }

            expectedPreviousHash = evt.CurrentHash;
        }

        return new EvidenceVerificationDto(
            evidence.Id,
            evidence.EvidenceNumber,
            true,
            events.Count,
            null,
            $"Chain of custody fully verified. All {events.Count} immutable events are cryptographically intact.");
    }
}
