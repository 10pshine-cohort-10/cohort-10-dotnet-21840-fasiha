using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TaskManagement.Api.Auth;

namespace TaskManagement.Api.Tasks;

[ApiController]
[Authorize]
[Route("api/tasks")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class TasksController(TaskService tasks, IOptions<JsonOptions> json) : ControllerBase
{
    [HttpGet]
    public Task<TaskPage> List([FromQuery] TaskFilter filter, CancellationToken cancellationToken) =>
        tasks.ListAsync(Actor, filter, cancellationToken);

    [HttpGet("counts")]
    public Task<TaskCounts> Counts(CancellationToken cancellationToken) =>
        tasks.CountsAsync(Actor, cancellationToken);

    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] TaskFilter filter, CancellationToken cancellationToken)
    {
        var file = await tasks.ExportAsync(Actor, filter, cancellationToken);
        return File(JsonSerializer.SerializeToUtf8Bytes(file, json.Value.JsonSerializerOptions),
            "application/json", "taskline-tasks.json");
    }

    [HttpPost("import")]
    [RequestSizeLimit(TaskFile.MaxBytes)]
    public async Task<IActionResult> Import(TaskFile file, CancellationToken cancellationToken)
    {
        var imported = await tasks.ImportAsync(Actor, file.Tasks!, cancellationToken);
        return Ok(new { imported });
    }

    [HttpGet("{id:guid}")]
    public Task<TaskDetail> Get(Guid id, CancellationToken cancellationToken) =>
        tasks.GetAsync(Actor, id, cancellationToken);

    [HttpPost]
    public async Task<ActionResult<TaskDetail>> Create(TaskInput input, CancellationToken cancellationToken)
    {
        var task = await tasks.CreateAsync(Actor, input, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = task.Id }, task);
    }

    [HttpPut("{id:guid}")]
    public Task<TaskDetail> Update(Guid id, EditTaskInput input, CancellationToken cancellationToken) =>
        tasks.UpdateAsync(Actor, id, input, cancellationToken);

    [HttpPatch("{id:guid}/status")]
    public Task<TaskDetail> ChangeStatus(Guid id, ChangeStatusInput input, CancellationToken cancellationToken) =>
        tasks.ChangeStatusAsync(Actor, id, input, cancellationToken);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery, Required] Guid? version, CancellationToken cancellationToken)
    {
        await tasks.DeleteAsync(Actor, id, version!.Value, cancellationToken);
        return NoContent();
    }

    private TaskActor Actor => new(User.FindFirstValue(ClaimTypes.NameIdentifier)!, User.IsInRole(RoleNames.Admin));
}
