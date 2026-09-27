using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TraceCore.Application.Common.Exceptions;
using TraceCore.Application.Common.Interfaces;
using TraceCore.Application.Common.Models;
using TraceCore.Domain.Entities.Tasks;
using TraceCore.Domain.Enums;
using TaskStatus = TraceCore.Domain.Enums.TaskStatus;

namespace TraceCore.Application.Tasks;

public sealed record TaskDto(
    Guid Id,
    Guid CaseId,
    Guid? InvestigationId,
    string Title,
    string Description,
    Guid? AssignedToUserId,
    TaskPriority Priority,
    TaskStatus Status,
    DateTime? DueDateUtc,
    DateTime? CompletedAtUtc,
    bool IsOverdue,
    DateTime CreatedAtUtc,
    string RowVersion);

// 1. Create Task
public sealed record CreateTaskCommand(
    Guid CaseId,
    string Title,
    string Description,
    TaskPriority Priority,
    Guid? AssignedToUserId,
    DateTime? DueDateUtc,
    Guid? InvestigationId = null) : IRequest<Guid>;

public class CreateTaskCommandValidator : AbstractValidator<CreateTaskCommand>
{
    public CreateTaskCommandValidator()
    {
        RuleFor(v => v.CaseId).NotEmpty();
        RuleFor(v => v.Title).NotEmpty().MaximumLength(200);
        RuleFor(v => v.Priority).IsInEnum();
    }
}

public class CreateTaskCommandHandler : IRequestHandler<CreateTaskCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public CreateTaskCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreateTaskCommand request, CancellationToken cancellationToken)
    {
        var task = CaseTask.Create(
            request.CaseId,
            request.Title,
            request.Description,
            request.Priority,
            request.AssignedToUserId,
            request.DueDateUtc,
            request.InvestigationId);

        _context.Add(task);
        await _context.SaveChangesAsync(cancellationToken);

        return task.Id;
    }
}

// 2. Update Task Status
public sealed record UpdateTaskStatusCommand(
    Guid TaskId,
    TaskStatus NewStatus,
    string? RowVersion = null) : IRequest<Unit>;

public class UpdateTaskStatusCommandValidator : AbstractValidator<UpdateTaskStatusCommand>
{
    public UpdateTaskStatusCommandValidator()
    {
        RuleFor(v => v.TaskId).NotEmpty();
        RuleFor(v => v.NewStatus).IsInEnum();
    }
}

public class UpdateTaskStatusCommandHandler : IRequestHandler<UpdateTaskStatusCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public UpdateTaskStatusCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(UpdateTaskStatusCommand request, CancellationToken cancellationToken)
    {
        var task = await _context.Tasks
            .FirstOrDefaultAsync(t => t.Id == request.TaskId, cancellationToken)
            ?? throw new NotFoundException(nameof(CaseTask), request.TaskId);

        if (!string.IsNullOrEmpty(request.RowVersion))
        {
            var requestVersion = ConcurrencyToken.DecodeOrNull(request.RowVersion)!;
            if (!task.RowVersion.SequenceEqual(requestVersion))
            {
                throw new ConcurrencyException("The task was modified concurrently. Please reload.");
            }
        }

        var userId = _currentUser.UserId ?? Guid.Parse("11111111-1111-1111-1111-111111111111");
        task.UpdateStatus(request.NewStatus, userId);

        await _context.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

// 3. Assign Task
public sealed record AssignTaskCommand(
    Guid TaskId,
    Guid AssignedToUserId,
    string? RowVersion = null) : IRequest<Unit>;

public class AssignTaskCommandValidator : AbstractValidator<AssignTaskCommand>
{
    public AssignTaskCommandValidator()
    {
        RuleFor(v => v.TaskId).NotEmpty();
        RuleFor(v => v.AssignedToUserId).NotEmpty();
    }
}

public class AssignTaskCommandHandler : IRequestHandler<AssignTaskCommand, Unit>
{
    private readonly IApplicationDbContext _context;

    public AssignTaskCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Unit> Handle(AssignTaskCommand request, CancellationToken cancellationToken)
    {
        var task = await _context.Tasks
            .FirstOrDefaultAsync(t => t.Id == request.TaskId, cancellationToken)
            ?? throw new NotFoundException(nameof(CaseTask), request.TaskId);

        if (!string.IsNullOrEmpty(request.RowVersion))
        {
            var requestVersion = ConcurrencyToken.DecodeOrNull(request.RowVersion)!;
            if (!task.RowVersion.SequenceEqual(requestVersion))
            {
                throw new ConcurrencyException("The task was modified concurrently. Please reload.");
            }
        }

        task.Assign(request.AssignedToUserId);
        await _context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

// 4. Get Tasks By Case Id
public sealed record GetTasksByCaseIdQuery(Guid CaseId) : IRequest<IReadOnlyList<TaskDto>>;

public class GetTasksByCaseIdQueryHandler : IRequestHandler<GetTasksByCaseIdQuery, IReadOnlyList<TaskDto>>
{
    private readonly IApplicationDbContext _context;

    public GetTasksByCaseIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<TaskDto>> Handle(GetTasksByCaseIdQuery request, CancellationToken cancellationToken)
    {
        return await _context.Tasks
            .AsNoTracking()
            .Where(t => t.CaseId == request.CaseId)
            .OrderByDescending(t => t.Priority)
            .ThenBy(t => t.DueDateUtc)
            .Take(200)
            .Select(t => new TaskDto(
                t.Id,
                t.CaseId,
                t.InvestigationId,
                t.Title,
                t.Description,
                t.AssignedToUserId,
                t.Priority,
                t.Status,
                t.DueDateUtc,
                t.CompletedAtUtc,
                t.DueDateUtc.HasValue && t.DueDateUtc.Value < DateTime.UtcNow && t.Status != TaskStatus.Completed && t.Status != TaskStatus.Cancelled,
                t.CreatedAtUtc,
                Convert.ToBase64String(t.RowVersion)))
            .ToListAsync(cancellationToken);
    }
}

// 5. Get Overdue Tasks
public sealed record GetOverdueTasksQuery : IRequest<IReadOnlyList<TaskDto>>;

public class GetOverdueTasksQueryHandler : IRequestHandler<GetOverdueTasksQuery, IReadOnlyList<TaskDto>>
{
    private readonly IApplicationDbContext _context;

    public GetOverdueTasksQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<TaskDto>> Handle(GetOverdueTasksQuery request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        return await _context.Tasks
            .AsNoTracking()
            .Where(t => t.DueDateUtc.HasValue &&
                        t.DueDateUtc.Value < now &&
                        t.Status != TaskStatus.Completed &&
                        t.Status != TaskStatus.Cancelled)
            .OrderBy(t => t.DueDateUtc)
            .Take(200)
            .Select(t => new TaskDto(
                t.Id,
                t.CaseId,
                t.InvestigationId,
                t.Title,
                t.Description,
                t.AssignedToUserId,
                t.Priority,
                t.Status,
                t.DueDateUtc,
                t.CompletedAtUtc,
                true,
                t.CreatedAtUtc,
                Convert.ToBase64String(t.RowVersion)))
            .ToListAsync(cancellationToken);
    }
}

// 6. Get My Tasks
public sealed record GetMyTasksQuery : IRequest<IReadOnlyList<TaskDto>>;

public class GetMyTasksQueryHandler : IRequestHandler<GetMyTasksQuery, IReadOnlyList<TaskDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetMyTasksQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<TaskDto>> Handle(GetMyTasksQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        if (!userId.HasValue)
            return [];

        var now = DateTime.UtcNow;
        return await _context.Tasks
            .AsNoTracking()
            .Where(t => t.AssignedToUserId == userId.Value && t.Status != TaskStatus.Completed && t.Status != TaskStatus.Cancelled)
            .OrderBy(t => t.DueDateUtc)
            .Take(200)
            .Select(t => new TaskDto(
                t.Id,
                t.CaseId,
                t.InvestigationId,
                t.Title,
                t.Description,
                t.AssignedToUserId,
                t.Priority,
                t.Status,
                t.DueDateUtc,
                t.CompletedAtUtc,
                t.DueDateUtc.HasValue && t.DueDateUtc.Value < now,
                t.CreatedAtUtc,
                Convert.ToBase64String(t.RowVersion)))
            .ToListAsync(cancellationToken);
    }
}
