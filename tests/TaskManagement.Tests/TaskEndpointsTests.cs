using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskManagement.Api.Auth;
using TaskManagement.Api.Data;
using TaskManagement.Api.Tasks;

namespace TaskManagement.Tests;

public sealed class TaskEndpointsTests(TaskApiFactory app) : IClassFixture<TaskApiFactory>
{
    [Fact]
    public async Task OwnerCanCreateFilterUpdateAndDelete()
    {
        using var client = await app.SignInNewUserAsync("owner@example.test", "Task Owner");
        var draft = new TaskInput
        {
            Title = "Prepare sprint plan",
            Description = "Confirm deliverables.",
            Category = "Planning",
            Priority = WorkPriority.High,
            DueDate = new DateOnly(2027, 1, 10)
        };
        using var created = await TaskApiFactory.SendAsync(client, HttpMethod.Post, "/api/tasks", draft);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var task = await ReadAsync<TaskDetail>(created);
        var path = $"/api/tasks/{task.Id}";
        Assert.EndsWith(path, created.Headers.Location!.ToString());
        Assert.True(task.CanManage);
        Assert.Equal(task.Creator, task.Assignee);
        Assert.Equal(draft.DueDate, task.DueDate);
        Assert.Equal(DateTimeKind.Utc, task.CreatedAt.Kind);

        var list = await GetAsync<TaskPage>(client, "/api/tasks?search=sprint&priority=High&status=Pending&category=Planning");
        Assert.Equal(task.Id, Assert.Single(list.Items).Id);
        Assert.Equal(1, list.Total);
        var empty = await GetAsync<TaskPage>(client, "/api/tasks?priority=Low");
        Assert.Empty(empty.Items);

        using var invalid = await TaskApiFactory.SendAsync(client, HttpMethod.Post, "/api/tasks", new { title = "   ", status = 1 });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        using var updated = await TaskApiFactory.SendAsync(client, HttpMethod.Put, path, new EditTaskInput
        {
            Title = "Publish sprint plan",
            AssigneeId = task.Assignee!.Id,
            Priority = WorkPriority.Low,
            Status = WorkStatus.Completed,
            Version = task.Version
        });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var newTask = await ReadAsync<TaskDetail>(updated);
        Assert.Equal(WorkStatus.Completed, newTask.Status);
        Assert.Equal("Publish sprint plan", newTask.Title);
        Assert.NotEqual(task.Version, newTask.Version);

        using var staleDelete = await TaskApiFactory.SendAsync(client, HttpMethod.Delete, path + $"?version={task.Version}");
        Assert.Equal(HttpStatusCode.Conflict, staleDelete.StatusCode);
        using var deleted = await TaskApiFactory.SendAsync(client, HttpMethod.Delete, path + $"?version={newTask.Version}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        using var missing = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Empty((await GetAsync<TaskPage>(client, "/api/tasks?search=sprint")).Items);
        Assert.Equal(0, (await GetAsync<TaskCounts>(client, "/api/tasks/counts")).Completed);

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var retained = await db.Tasks.IgnoreQueryFilters().SingleAsync(item => item.Id == task.Id);
        Assert.True(retained.IsDeleted);
        Assert.Equal("Publish sprint plan", retained.Title);
    }

    [Fact]
    public async Task AdminCanDelegateAndAssigneeCanUpdateOnlyStatus()
    {
        using var admin = await app.SignInAdminAsync("admin@example.test");
        using var worker = await app.SignInNewUserAsync("worker@example.test", "Assigned User");
        using var outsider = await app.SignInNewUserAsync("outsider@example.test", "Other User");
        var assignees = await GetAsync<AssigneeOption[]>(admin, "/api/account/assignees?search=worker");
        var assignee = Assert.Single(assignees);

        using var forbidden = await TaskApiFactory.SendAsync(outsider, HttpMethod.Post, "/api/tasks",
            new TaskInput { Title = "Peer assignment", AssigneeId = assignee.Id });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        using var created = await TaskApiFactory.SendAsync(admin, HttpMethod.Post, "/api/tasks",
            new TaskInput { Title = "Review delegated report", AssigneeId = assignee.Id });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var task = await ReadAsync<TaskDetail>(created);
        var path = $"/api/tasks/{task.Id}";

        using var outsideRead = await outsider.GetAsync(path);
        Assert.Equal(HttpStatusCode.NotFound, outsideRead.StatusCode);
        var outsiderTasks = await GetAsync<TaskPage>(outsider, "/api/tasks?search=delegated");
        Assert.Empty(outsiderTasks.Items);

        var assignedView = await GetAsync<TaskDetail>(worker, path);
        Assert.False(assignedView.CanManage);
        Assert.Equal(assignee.Id, assignedView.Assignee?.Id);
        using var forbiddenDelete = await TaskApiFactory.SendAsync(worker, HttpMethod.Delete, path + $"?version={task.Version}");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenDelete.StatusCode);

        using var changed = await TaskApiFactory.SendAsync(worker, HttpMethod.Patch, path + "/status",
            new ChangeStatusInput(WorkStatus.InProgress, task.Version));
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.Equal(WorkStatus.InProgress, (await ReadAsync<TaskDetail>(changed)).Status);

        using var stale = await TaskApiFactory.SendAsync(admin, HttpMethod.Patch, path + "/status",
            new ChangeStatusInput(WorkStatus.Completed, task.Version));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var problem = await ReadAsync<ProblemDetails>(stale);
        Assert.Contains("Reload", problem.Title);
        Assert.Contains("traceId", problem.Extensions.Keys);

        Assert.Equal(1, (await GetAsync<TaskCounts>(admin, "/api/tasks/counts")).InProgress);
        Assert.Equal(1, (await GetAsync<TaskCounts>(worker, "/api/tasks/counts")).InProgress);
        Assert.Equal(0, (await GetAsync<TaskCounts>(outsider, "/api/tasks/counts")).InProgress);
    }

    private static async Task<T> GetAsync<T>(HttpClient client, string path) =>
        (await client.GetFromJsonAsync<T>(path, TaskApiFactory.Json))!;

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(TaskApiFactory.Json))!;
}
