using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TraceCore.Application.Common.Exceptions;
using TraceCore.Application.Common.Interfaces;
using TraceCore.Domain.Common.Exceptions;
using TraceCore.Domain.Entities.Cases;
using TraceCore.Domain.Enums;

namespace TraceCore.Application.Cases.Commands;

// 1. Create Case
public sealed record CreateCaseCommand(
    string Title,
    string Description,
    CaseType Type,
    CasePriority Priority,
    ConfidentialityLevel ConfidentialityLevel,
    int SlaTargetHours,
    DateTime? DueDateUtc = null,
    bool StartAsOpen = true) : IRequest<CreateCaseResult>;

public sealed record CreateCaseResult(Guid CaseId, string CaseNumber);

public class CreateCaseCommandValidator : AbstractValidator<CreateCaseCommand>
{
    public CreateCaseCommandValidator()
    {
        RuleFor(v => v.Title).NotEmpty().MaximumLength(200);
        RuleFor(v => v.Description).NotEmpty();
        RuleFor(v => v.Type).IsInEnum();
        RuleFor(v => v.Priority).IsInEnum();
        RuleFor(v => v.ConfidentialityLevel).IsInEnum();
        RuleFor(v => v.SlaTargetHours).GreaterThan(0);
    }
}

public class CreateCaseCommandHandler : IRequestHandler<CreateCaseCommand, CreateCaseResult>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ISlaCalculationService _slaService;
    private readonly IDateTimeProvider _dateTime;

    public CreateCaseCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        ISlaCalculationService slaService,
        IDateTimeProvider dateTime)
    {
        _context = context;
        _currentUser = currentUser;
        _slaService = slaService;
        _dateTime = dateTime;
    }

    public async Task<CreateCaseResult> Handle(CreateCaseCommand request, CancellationToken cancellationToken)
    {
        var currentYear = _dateTime.UtcNow.Year;
        var existingCount = await _context.Cases.CountAsync(cancellationToken);
        string caseNumber = $"CAS-{currentYear}-{(existingCount + 1):D4}";

        var userId = _currentUser.UserId ?? Guid.Parse("11111111-1111-1111-1111-111111111111");
        int slaHours = request.SlaTargetHours > 0
            ? request.SlaTargetHours
            : _slaService.GetTargetHours(request.Priority, request.Type);

        var @case = request.StartAsOpen
            ? Case.CreateOpen(
                caseNumber,
                request.Title,
                request.Description,
                request.Type,
                request.Priority,
                request.ConfidentialityLevel,
                userId,
                slaHours,
                request.DueDateUtc)
            : Case.CreateDraft(
                caseNumber,
                request.Title,
                request.Description,
                request.Type,
                request.Priority,
                request.ConfidentialityLevel,
                userId,
                slaHours,
                request.DueDateUtc);

        // Grant creator Admin access to the case
        @case.AddAccessGrant(userId, CaseAccessLevel.Admin, userId);

        _context.Add(@case);
        await _context.SaveChangesAsync(cancellationToken);

        return new CreateCaseResult(@case.Id, @case.CaseNumber);
    }
}

// 2. Update Case
public sealed record UpdateCaseCommand(
    Guid Id,
    string Title,
    string Description,
    CaseType Type,
    ConfidentialityLevel ConfidentialityLevel,
    string? RowVersion = null) : IRequest<Unit>;

public class UpdateCaseCommandValidator : AbstractValidator<UpdateCaseCommand>
{
    public UpdateCaseCommandValidator()
    {
        RuleFor(v => v.Id).NotEmpty();
        RuleFor(v => v.Title).NotEmpty().MaximumLength(200);
        RuleFor(v => v.Description).NotEmpty();
    }
}

public class UpdateCaseCommandHandler : IRequestHandler<UpdateCaseCommand, Unit>
{
    private readonly IApplicationDbContext _context;

    public UpdateCaseCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Unit> Handle(UpdateCaseCommand request, CancellationToken cancellationToken)
    {
        var @case = await _context.Cases
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Case), request.Id);

        if (!string.IsNullOrEmpty(request.RowVersion))
        {
            var requestVersion = Convert.FromBase64String(request.RowVersion);
            if (!@case.RowVersion.SequenceEqual(requestVersion))
            {
                throw new ConcurrencyException("The case was modified by another user. Please reload.");
            }
        }

        @case.UpdateDetails(request.Title, request.Description, request.Type, request.ConfidentialityLevel);
        await _context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

// 3. Change Case Status
public sealed record ChangeCaseStatusCommand(
    Guid Id,
    CaseStatus NewStatus,
    string Reason,
    string? RowVersion = null) : IRequest<Unit>;

public class ChangeCaseStatusCommandValidator : AbstractValidator<ChangeCaseStatusCommand>
{
    public ChangeCaseStatusCommandValidator()
    {
        RuleFor(v => v.Id).NotEmpty();
        RuleFor(v => v.NewStatus).IsInEnum();
        RuleFor(v => v.Reason).NotEmpty().MaximumLength(500);
    }
}

public class ChangeCaseStatusCommandHandler : IRequestHandler<ChangeCaseStatusCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ChangeCaseStatusCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(ChangeCaseStatusCommand request, CancellationToken cancellationToken)
    {
        var @case = await _context.Cases
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Case), request.Id);

        if (!string.IsNullOrEmpty(request.RowVersion))
        {
            var requestVersion = Convert.FromBase64String(request.RowVersion);
            if (!@case.RowVersion.SequenceEqual(requestVersion))
            {
                throw new ConcurrencyException("The case was modified by another user. Please reload.");
            }
        }

        var userId = _currentUser.UserId ?? Guid.Parse("11111111-1111-1111-1111-111111111111");
        @case.TransitionStatus(request.NewStatus, userId, request.Reason);

        await _context.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

// 4. Assign Case
public sealed record AssignCaseCommand(
    Guid Id,
    Guid InvestigatorId,
    Guid? TeamId = null,
    string? RowVersion = null) : IRequest<Unit>;

public class AssignCaseCommandValidator : AbstractValidator<AssignCaseCommand>
{
    public AssignCaseCommandValidator()
    {
        RuleFor(v => v.Id).NotEmpty();
        RuleFor(v => v.InvestigatorId).NotEmpty();
    }
}

public class AssignCaseCommandHandler : IRequestHandler<AssignCaseCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public AssignCaseCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(AssignCaseCommand request, CancellationToken cancellationToken)
    {
        var @case = await _context.Cases
            .Include(c => c.AccessGrants)
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Case), request.Id);

        if (!string.IsNullOrEmpty(request.RowVersion))
        {
            var requestVersion = Convert.FromBase64String(request.RowVersion);
            if (!@case.RowVersion.SequenceEqual(requestVersion))
            {
                throw new ConcurrencyException("The case was modified by another user. Please reload.");
            }
        }

        var userId = _currentUser.UserId ?? Guid.Parse("11111111-1111-1111-1111-111111111111");
        @case.AssignInvestigator(request.InvestigatorId, userId, request.TeamId);

        // Grant investigator ReadWrite access to the case
        @case.AddAccessGrant(request.InvestigatorId, CaseAccessLevel.ReadWrite, userId, request.TeamId);

        await _context.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

// 5. Update Priority
public sealed record UpdateCasePriorityCommand(
    Guid Id,
    CasePriority NewPriority,
    string Reason,
    string? RowVersion = null) : IRequest<Unit>;

public class UpdateCasePriorityCommandValidator : AbstractValidator<UpdateCasePriorityCommand>
{
    public UpdateCasePriorityCommandValidator()
    {
        RuleFor(v => v.Id).NotEmpty();
        RuleFor(v => v.NewPriority).IsInEnum();
        RuleFor(v => v.Reason).NotEmpty().MaximumLength(500);
    }
}

public class UpdateCasePriorityCommandHandler : IRequestHandler<UpdateCasePriorityCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public UpdateCasePriorityCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(UpdateCasePriorityCommand request, CancellationToken cancellationToken)
    {
        var @case = await _context.Cases
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Case), request.Id);

        if (!string.IsNullOrEmpty(request.RowVersion))
        {
            var requestVersion = Convert.FromBase64String(request.RowVersion);
            if (!@case.RowVersion.SequenceEqual(requestVersion))
            {
                throw new ConcurrencyException("The case was modified by another user. Please reload.");
            }
        }

        var userId = _currentUser.UserId ?? Guid.Parse("11111111-1111-1111-1111-111111111111");
        @case.UpdatePriority(request.NewPriority, userId, request.Reason);

        await _context.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
