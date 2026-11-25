using TaskManagement.Api.Data;

namespace TaskManagement.Api.Tasks;

public sealed class TaskService(TaskStore store, TimeProvider clock, ILogger<TaskService> logger)
{
    public Task<TaskPage> ListAsync(TaskActor actor, TaskFilter filter, CancellationToken cancellationToken) =>
        store.ListAsync(actor, filter, cancellationToken);

    public Task<TaskCounts> CountsAsync(TaskActor actor, CancellationToken cancellationToken) =>
        store.CountsAsync(actor, cancellationToken);

    public async Task<TaskDetail> GetAsync(TaskActor actor, Guid id, CancellationToken cancellationToken) =>
        Describe(await FindAsync(actor, id, cancellationToken), actor);

    public async Task<TaskDetail> CreateAsync(TaskActor actor, TaskInput input, CancellationToken cancellationToken)
    {
        var creator = await FindUserAsync(actor.Id, cancellationToken);
        var assigneeId = input.AssigneeId ?? (actor.IsAdmin ? null : actor.Id);
        if (!actor.IsAdmin && assigneeId != actor.Id)
            throw new TaskProblem(StatusCodes.Status403Forbidden, "Only an administrator can assign tasks to another account.");

        var now = clock.GetUtcNow().UtcDateTime;
        var task = new WorkTask { CreatorId = creator.Id, Creator = creator, CreatedAt = now };
        ApplyDetails(task, input, assigneeId is null ? null : await FindUserAsync(assigneeId, cancellationToken), now);
        store.Add(task);
        await store.SaveAsync(cancellationToken);
        logger.LogInformation("Account {UserId} created task {TaskId} assigned to {AssigneeId}", actor.Id, task.Id, task.AssigneeId);
        return Describe(task, actor);
    }

    public async Task<TaskDetail> UpdateAsync(
        TaskActor actor, Guid id, EditTaskInput input, CancellationToken cancellationToken)
    {
        var task = await FindAsync(actor, id, cancellationToken);
        RequireManager(task, actor);
        RequireCurrentVersion(task, input.Version);
        if (!actor.IsAdmin && input.AssigneeId != task.AssigneeId)
            throw new TaskProblem(StatusCodes.Status403Forbidden, "Only an administrator can change a task assignment.");

        var assignee = input.AssigneeId == task.AssigneeId ? task.Assignee
            : input.AssigneeId is null ? null : await FindUserAsync(input.AssigneeId, cancellationToken);
        ApplyDetails(task, input, assignee, clock.GetUtcNow().UtcDateTime);
        await store.SaveAsync(cancellationToken);
        logger.LogInformation("Account {UserId} updated task {TaskId} assigned to {AssigneeId}", actor.Id, id, task.AssigneeId);
        return Describe(task, actor);
    }

    public async Task<TaskDetail> ChangeStatusAsync(
        TaskActor actor, Guid id, ChangeStatusInput input, CancellationToken cancellationToken)
    {
        var task = await FindAsync(actor, id, cancellationToken);
        RequireCurrentVersion(task, input.Version);
        task.Status = input.Status!.Value;
        Touch(task, clock.GetUtcNow().UtcDateTime);
        await store.SaveAsync(cancellationToken);
        logger.LogInformation("Account {UserId} changed task {TaskId} status to {Status}", actor.Id, id, task.Status);
        return Describe(task, actor);
    }

    public async Task DeleteAsync(TaskActor actor, Guid id, Guid version, CancellationToken cancellationToken)
    {
        var task = await FindAsync(actor, id, cancellationToken);
        RequireManager(task, actor);
        RequireCurrentVersion(task, version);
        store.Remove(task);
        await store.SaveAsync(cancellationToken);
        logger.LogInformation("Account {UserId} deleted task {TaskId}", actor.Id, id);
    }

    private async Task<WorkTask> FindAsync(TaskActor actor, Guid id, CancellationToken cancellationToken) =>
        await store.FindAsync(actor, id, cancellationToken)
        ?? throw new TaskProblem(StatusCodes.Status404NotFound, "Task could not be found.");

    private async Task<AppUser> FindUserAsync(string id, CancellationToken cancellationToken) =>
        await store.FindUserAsync(id, cancellationToken)
        ?? throw new TaskProblem(StatusCodes.Status400BadRequest, "The selected account could not be found.");

    private static void RequireManager(WorkTask task, TaskActor actor)
    {
        if (!actor.CanManage(task))
            throw new TaskProblem(StatusCodes.Status403Forbidden, "Only the creator or an administrator can manage this task.");
    }

    private static void RequireCurrentVersion(WorkTask task, Guid? version)
    {
        if (task.Version != version)
            throw new TaskProblem(StatusCodes.Status409Conflict, "This task changed. Reload it and try again.");
    }

    private static void ApplyDetails(WorkTask task, TaskInput input, AppUser? assignee, DateTime now)
    {
        task.Title = input.Title.Trim();
        task.Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
        task.Category = string.IsNullOrWhiteSpace(input.Category) ? null : input.Category.Trim();
        task.DueDate = input.DueDate;
        task.Priority = input.Priority;
        task.Status = input.Status;
        task.Assignee = assignee;
        task.AssigneeId = assignee?.Id;
        Touch(task, now);
    }

    private static void Touch(WorkTask task, DateTime now)
    {
        task.UpdatedAt = now;
        task.Version = Guid.NewGuid();
    }

    private static TaskDetail Describe(WorkTask task, TaskActor actor) =>
        new(task.Id, task.Title, task.Description, task.Category, task.Status, task.Priority, task.DueDate,
            new TaskPerson(task.Creator.Id, task.Creator.DisplayName),
            task.Assignee is { } assignee ? new TaskPerson(assignee.Id, assignee.DisplayName) : null,
            task.CreatedAt, task.UpdatedAt, task.Version, actor.CanManage(task));
}
