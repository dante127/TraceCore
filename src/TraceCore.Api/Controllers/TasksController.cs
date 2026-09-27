using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TraceCore.Api.Common;
using TraceCore.Api.Services;
using TraceCore.Application.Common.Interfaces;
using TraceCore.Application.Tasks;
using TraceCore.Domain.Enums;
using TraceCore.Infrastructure.Security;

namespace TraceCore.Api.Controllers;

[Authorize]
public class TasksController : ApiControllerBase
{
    private readonly ICaseAuthorizationService _caseAuth;
    private readonly IApplicationDbContext _context;

    public TasksController(ISender sender, ICaseAuthorizationService caseAuth, IApplicationDbContext context)
        : base(sender)
    {
        _caseAuth = caseAuth;
        _context = context;
    }

    [HttpGet]
    [HasPermission(Permissions.TaskRead)]
    public async Task<ActionResult<IReadOnlyList<TaskDto>>> GetTasksByCase([FromQuery] Guid caseId, CancellationToken ct)
    {
        if (!await _caseAuth.HasCaseAccessAsync(caseId, CaseAccessLevel.Read, ct))
        {
            return Forbid();
        }

        return Ok(await Sender.Send(new GetTasksByCaseIdQuery(caseId), ct));
    }

    [HttpPost]
    [HasPermission(Permissions.TaskWrite)]
    public async Task<ActionResult<Guid>> CreateTask([FromBody] CreateTaskCommand command, CancellationToken ct)
    {
        if (!await _caseAuth.HasCaseAccessAsync(command.CaseId, CaseAccessLevel.ReadWrite, ct))
        {
            return Forbid();
        }

        var id = await Sender.Send(command, ct);
        return StatusCode(StatusCodes.Status201Created, id);
    }

    [HttpPut("{id:guid}/status")]
    [HasPermission(Permissions.TaskWrite)]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateTaskStatusCommand command, CancellationToken ct)
    {
        if (id != command.TaskId)
            return BadRequest("Route ID does not match command ID.");

        var caseId = await ResolveCaseIdAsync(id, ct);
        if (caseId is null)
            return NotFound();

        if (!await _caseAuth.HasCaseAccessAsync(caseId.Value, CaseAccessLevel.ReadWrite, ct))
        {
            return Forbid();
        }

        await Sender.Send(command, ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/assign")]
    [HasPermission(Permissions.TaskWrite)]
    public async Task<IActionResult> Assign(Guid id, [FromBody] AssignTaskCommand command, CancellationToken ct)
    {
        if (id != command.TaskId)
            return BadRequest("Route ID does not match command ID.");

        var caseId = await ResolveCaseIdAsync(id, ct);
        if (caseId is null)
            return NotFound();

        if (!await _caseAuth.HasCaseAccessAsync(caseId.Value, CaseAccessLevel.ReadWrite, ct))
        {
            return Forbid();
        }

        await Sender.Send(command, ct);
        return NoContent();
    }

    [HttpGet("overdue")]
    [HasPermission(Permissions.TaskWrite)]
    public async Task<ActionResult<IReadOnlyList<TaskDto>>> GetOverdueTasks(CancellationToken ct)
    {
        return Ok(await Sender.Send(new GetOverdueTasksQuery(), ct));
    }

    [HttpGet("my")]
    public async Task<ActionResult<IReadOnlyList<TaskDto>>> GetMyTasks(CancellationToken ct)
    {
        return Ok(await Sender.Send(new GetMyTasksQuery(), ct));
    }

    private async Task<Guid?> ResolveCaseIdAsync(Guid taskId, CancellationToken ct)
    {
        return await _context.Tasks
            .AsNoTracking()
            .Where(t => t.Id == taskId)
            .Select(t => (Guid?)t.CaseId)
            .FirstOrDefaultAsync(ct);
    }
}