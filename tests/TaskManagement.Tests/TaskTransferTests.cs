using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TaskManagement.Api.Auth;
using TaskManagement.Api.Tasks;

namespace TaskManagement.Tests;

public sealed class TaskTransferTests(TaskApiFactory app) : IClassFixture<TaskApiFactory>
{
    [SqlServerFact]
    public async Task ExportHonorsFiltersAndVisibilityAndCanBeImportedAsCopies()
    {
        using var admin = await app.SignInAdminAsync("transfer-admin@example.test");
        using var user = await app.SignInNewUserAsync("transfer-user@example.test", "Export User");
        var account = (await user.GetFromJsonAsync<AccountProfile>("/api/account/me"))!;
        var draft = new TaskInput
        {
            Title = "Quarterly review",
            Description = "Keep these notes.",
            Category = "Planning",
            Status = WorkStatus.InProgress,
            Priority = WorkPriority.High,
            DueDate = new DateOnly(2027, 1, 15)
        };
        var original = await CreateAsync(user, draft);
        await CreateAsync(admin, new TaskInput { Title = "Quarterly confidential" });
        await CreateAsync(user, new TaskInput { Title = "Unrelated personal task" });
        var deleted = await CreateAsync(user, new TaskInput { Title = "Quarterly deleted" });
        using var removed = await TaskApiFactory.SendAsync(user, HttpMethod.Delete,
            $"/api/tasks/{deleted.Id}?version={deleted.Version}");
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);

        using var download = await user.GetAsync("/api/tasks/export?search=Quarterly");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("taskline-tasks.json", download.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        var json = await download.Content.ReadAsStringAsync();
        using var parsed = JsonDocument.Parse(json);
        var row = Assert.Single(parsed.RootElement.GetProperty("tasks").EnumerateArray());
        Assert.False(row.TryGetProperty("id", out _));
        Assert.False(row.TryGetProperty("assigneeId", out _));
        var file = JsonSerializer.Deserialize<TaskFile>(json, TaskApiFactory.Json)!;
        Assert.Equal(1, file.Version);
        Assert.Equal(draft.Title, Assert.Single(file.Tasks!)!.Title);

        using var uploaded = await TaskApiFactory.SendAsync(user, HttpMethod.Post, "/api/tasks/import", file);
        Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode);
        var result = await uploaded.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, result.GetProperty("imported").GetInt32());
        var list = (await user.GetFromJsonAsync<TaskPage>("/api/tasks?search=Quarterly", TaskApiFactory.Json))!;
        Assert.Equal(2, list.Total);
        var copyId = Assert.Single(list.Items, item => item.Id != original.Id).Id;
        var copy = (await user.GetFromJsonAsync<TaskDetail>($"/api/tasks/{copyId}", TaskApiFactory.Json))!;
        Assert.Equal(draft.Description, copy.Description);
        Assert.Equal(draft.Category, copy.Category);
        Assert.Equal(draft.DueDate, copy.DueDate);
        Assert.Equal(draft.Status, copy.Status);
        Assert.Equal(draft.Priority, copy.Priority);
        Assert.Equal(account.Id, copy.Creator.Id);
        Assert.Equal(account.Id, copy.Assignee?.Id);
    }

    [SqlServerFact]
    public async Task InvalidImportRejectsTheWholeFile()
    {
        using var user = await app.SignInNewUserAsync("invalid-import@example.test", "Import User");
        using var invalid = await TaskApiFactory.SendAsync(user, HttpMethod.Post, "/api/tasks/import", new
        {
            version = 1,
            tasks = new[] { new { title = "Valid title" }, new { title = " " } }
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        using var unsupported = await TaskApiFactory.SendAsync(user, HttpMethod.Post, "/api/tasks/import", new
        {
            version = 2,
            tasks = new[] { new { title = "Valid title" } }
        });
        Assert.Equal(HttpStatusCode.BadRequest, unsupported.StatusCode);
        var list = (await user.GetFromJsonAsync<TaskPage>("/api/tasks", TaskApiFactory.Json))!;
        Assert.Empty(list.Items);
    }

    private static async Task<TaskDetail> CreateAsync(HttpClient client, TaskInput task)
    {
        using var response = await TaskApiFactory.SendAsync(client, HttpMethod.Post, "/api/tasks", task);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TaskDetail>(TaskApiFactory.Json))!;
    }
}
