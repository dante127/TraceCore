using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TraceCore.Application.Tasks;

namespace TraceCore.Api.Controllers;

[Authorize]
public class TasksController : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TaskDto>>> GetTasksByCase([FromQuery] Guid caseId, CancellationToken ct)
    {
        return Ok(await Sender.Send(new GetTasksByCaseIdQuery(caseId), ct));
    }

    [HttpPost]
    public async Task<ActionResult<Guid>> CreateTask([FromBody] CreateTaskCommand command, CancellationToken ct)
    {
        var id = await Sender.Send(command, ct);
        return Ok(id);
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateTaskStatusCommand command, CancellationToken ct)
    {
        if (id != command.TaskId)
            return BadRequest("Route ID does not match command ID.");

        await Sender.Send(command, ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/assign")]
    public async Task<IActionResult> Assign(Guid id, [FromBody] AssignTaskCommand command, CancellationToken ct)
    {
        if (id != command.TaskId)
            return BadRequest("Route ID does not match command ID.");

        await Sender.Send(command, ct);
        return NoContent();
    }

    [HttpGet("overdue")]
    public async Task<ActionResult<IReadOnlyList<TaskDto>>> GetOverdueTasks(CancellationToken ct)
    {
        return Ok(await Sender.Send(new GetOverdueTasksQuery(), ct));
    }

    [HttpGet("my")]
    public async Task<ActionResult<IReadOnlyList<TaskDto>>> GetMyTasks(CancellationToken ct)
    {
        return Ok(await Sender.Send(new GetMyTasksQuery(), ct));
    }
}
