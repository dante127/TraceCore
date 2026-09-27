using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TraceCore.Application.Common.Exceptions;
using TraceCore.Application.Common.Interfaces;
using TraceCore.Domain.Entities.Investigations;
using TraceCore.Domain.Enums;

namespace TraceCore.Application.Investigations;

public sealed record InvestigationActivityDto(
    Guid Id,
    Guid InvestigationId,
    ActivityType ActivityType,
    Guid PerformedByUserId,
    DateTime PerformedAtUtc,
    string Description,
    string Outcome,
    string Location,
    string Notes,
    DateTime CreatedAtUtc);

public sealed record InvestigationDto(
    Guid Id,
    Guid CaseId,
    string Title,
    Guid LeadInvestigatorId,
    InvestigationStatus Status,
    DateTime? StartDateUtc,
    DateTime? EndDateUtc,
    DateTime CreatedAtUtc,
    string RowVersion);

public sealed record InvestigationDetailDto(
    Guid Id,
    Guid CaseId,
    string Title,
    Guid LeadInvestigatorId,
    string Objectives,
    string Findings,
    InvestigationStatus Status,
    DateTime? StartDateUtc,
    DateTime? EndDateUtc,
    DateTime CreatedAtUtc,
    string RowVersion,
    IReadOnlyList<InvestigationActivityDto> Activities);

// 1. Create Investigation
public sealed record CreateInvestigationCommand(
    Guid CaseId,
    string Title,
    Guid LeadInvestigatorId,
    string Objectives,
    bool StartImmediately = true) : IRequest<Guid>;

public class CreateInvestigationCommandValidator : AbstractValidator<CreateInvestigationCommand>
{
    public CreateInvestigationCommandValidator()
    {
        RuleFor(v => v.CaseId).NotEmpty();
        RuleFor(v => v.Title).NotEmpty().MaximumLength(200);
        RuleFor(v => v.LeadInvestigatorId).NotEmpty();
    }
}

public class CreateInvestigationCommandHandler : IRequestHandler<CreateInvestigationCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public CreateInvestigationCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreateInvestigationCommand request, CancellationToken cancellationToken)
    {
        var investigation = Investigation.Create(
            request.CaseId,
            request.Title,
            request.LeadInvestigatorId,
            request.Objectives,
            request.StartImmediately);

        _context.Add(investigation);
        await _context.SaveChangesAsync(cancellationToken);

        return investigation.Id;
    }
}

// 2. Add Activity
public sealed record AddInvestigationActivityCommand(
    Guid InvestigationId,
    ActivityType ActivityType,
    DateTime PerformedAtUtc,
    string Description,
    string Outcome,
    string Location = "",
    string Notes = "") : IRequest<Guid>;

public class AddInvestigationActivityCommandValidator : AbstractValidator<AddInvestigationActivityCommand>
{
    public AddInvestigationActivityCommandValidator()
    {
        RuleFor(v => v.InvestigationId).NotEmpty();
        RuleFor(v => v.ActivityType).IsInEnum();
        RuleFor(v => v.Description).NotEmpty().MaximumLength(500);
    }
}

public class AddInvestigationActivityCommandHandler : IRequestHandler<AddInvestigationActivityCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public AddInvestigationActivityCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Guid> Handle(AddInvestigationActivityCommand request, CancellationToken cancellationToken)
    {
        var investigation = await _context.Investigations
            .Include(i => i.Activities)
            .FirstOrDefaultAsync(i => i.Id == request.InvestigationId, cancellationToken)
            ?? throw new NotFoundException(nameof(Investigation), request.InvestigationId);

        var userId = _currentUser.UserId ?? Guid.Parse("11111111-1111-1111-1111-111111111111");

        var activity = investigation.AddActivity(
            request.ActivityType,
            userId,
            request.PerformedAtUtc,
            request.Description,
            request.Outcome,
            request.Location,
            request.Notes);

        _context.Add(activity);

        await _context.SaveChangesAsync(cancellationToken);
        return activity.Id;
    }
}

// 3. Update Findings
public sealed record UpdateInvestigationFindingsCommand(
    Guid InvestigationId,
    string Findings,
    string? RowVersion = null) : IRequest<Unit>;

public class UpdateInvestigationFindingsCommandHandler : IRequestHandler<UpdateInvestigationFindingsCommand, Unit>
{
    private readonly IApplicationDbContext _context;

    public UpdateInvestigationFindingsCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Unit> Handle(UpdateInvestigationFindingsCommand request, CancellationToken cancellationToken)
    {
        var investigation = await _context.Investigations
            .FirstOrDefaultAsync(i => i.Id == request.InvestigationId, cancellationToken)
            ?? throw new NotFoundException(nameof(Investigation), request.InvestigationId);

        if (!string.IsNullOrEmpty(request.RowVersion))
        {
            var requestVersion = Convert.FromBase64String(request.RowVersion);
            if (!investigation.RowVersion.SequenceEqual(requestVersion))
            {
                throw new ConcurrencyException("The investigation was modified concurrently. Please reload.");
            }
        }

        investigation.UpdateFindings(request.Findings);
        await _context.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

// 4. Update Status
public sealed record UpdateInvestigationStatusCommand(
    Guid InvestigationId,
    InvestigationStatus NewStatus,
    string? FinalFindings = null,
    string? RowVersion = null) : IRequest<Unit>;

public class UpdateInvestigationStatusCommandHandler : IRequestHandler<UpdateInvestigationStatusCommand, Unit>
{
    private readonly IApplicationDbContext _context;

    public UpdateInvestigationStatusCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Unit> Handle(UpdateInvestigationStatusCommand request, CancellationToken cancellationToken)
    {
        var investigation = await _context.Investigations
            .FirstOrDefaultAsync(i => i.Id == request.InvestigationId, cancellationToken)
            ?? throw new NotFoundException(nameof(Investigation), request.InvestigationId);

        if (!string.IsNullOrEmpty(request.RowVersion))
        {
            var requestVersion = Convert.FromBase64String(request.RowVersion);
            if (!investigation.RowVersion.SequenceEqual(requestVersion))
            {
                throw new ConcurrencyException("The investigation was modified concurrently. Please reload.");
            }
        }

        switch (request.NewStatus)
        {
            case InvestigationStatus.Active:
                investigation.Start(investigation.LeadInvestigatorId);
                break;
            case InvestigationStatus.Completed:
                investigation.Complete(request.FinalFindings ?? investigation.Findings);
                break;
            case InvestigationStatus.Suspended:
                investigation.Suspend("Investigation suspended by request.");
                break;
            case InvestigationStatus.Closed:
                investigation.Close();
                break;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

// 5. Get Investigation By Id
public sealed record GetInvestigationByIdQuery(Guid Id) : IRequest<InvestigationDetailDto>;

public class GetInvestigationByIdQueryHandler : IRequestHandler<GetInvestigationByIdQuery, InvestigationDetailDto>
{
    private readonly IApplicationDbContext _context;

    public GetInvestigationByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<InvestigationDetailDto> Handle(GetInvestigationByIdQuery request, CancellationToken cancellationToken)
    {
        var inv = await _context.Investigations
            .Include(i => i.Activities)
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Investigation), request.Id);

        var activities = inv.Activities
            .OrderByDescending(a => a.PerformedAtUtc)
            .Select(a => new InvestigationActivityDto(
                a.Id,
                a.InvestigationId,
                a.ActivityType,
                a.PerformedByUserId,
                a.PerformedAtUtc,
                a.Description,
                a.Outcome,
                a.Location,
                a.Notes,
                a.CreatedAtUtc))
            .ToList();

        return new InvestigationDetailDto(
            inv.Id,
            inv.CaseId,
            inv.Title,
            inv.LeadInvestigatorId,
            inv.Objectives,
            inv.Findings,
            inv.Status,
            inv.StartDateUtc,
            inv.EndDateUtc,
            inv.CreatedAtUtc,
            Convert.ToBase64String(inv.RowVersion),
            activities);
    }
}

// 6. Get Investigations By Case Id
public sealed record GetInvestigationsByCaseIdQuery(Guid CaseId) : IRequest<IReadOnlyList<InvestigationDto>>;

public class GetInvestigationsByCaseIdQueryHandler : IRequestHandler<GetInvestigationsByCaseIdQuery, IReadOnlyList<InvestigationDto>>
{
    private readonly IApplicationDbContext _context;

    public GetInvestigationsByCaseIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<InvestigationDto>> Handle(GetInvestigationsByCaseIdQuery request, CancellationToken cancellationToken)
    {
        return await _context.Investigations
            .AsNoTracking()
            .Where(i => i.CaseId == request.CaseId)
            .OrderByDescending(i => i.CreatedAtUtc)
            .Select(i => new InvestigationDto(
                i.Id,
                i.CaseId,
                i.Title,
                i.LeadInvestigatorId,
                i.Status,
                i.StartDateUtc,
                i.EndDateUtc,
                i.CreatedAtUtc,
                Convert.ToBase64String(i.RowVersion)))
            .ToListAsync(cancellationToken);
    }
}
