using System.Net;
using System.Net.Http.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using TaskManagement.Api.Auth;
using TaskManagement.Api.Tasks;

namespace TaskManagement.Tests;

public sealed class TaskUpdatesTests(TaskApiFactory app) : IClassFixture<TaskApiFactory>
{
    [SqlServerFact]
    public async Task StatusChangeNotifiesAssigneeAndKeepsUnrelatedUserOut()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var anonymous = app.CreateSession();
        using var rejected = await anonymous.PostAsync("/api/task-updates/negotiate", null, timeout.Token);
        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);

        const string assigneeEmail = "live-assignee@example.test";
        const string outsiderEmail = "live-outsider@example.test";
        using var admin = await app.SignInAdminAsync("live-admin@example.test");
        using var assignee = await app.SignInNewUserAsync(assigneeEmail, "Live Assignee");
        using var outsider = await app.SignInNewUserAsync(outsiderEmail, "Live Outsider");
        var profile = (await assignee.GetFromJsonAsync<AccountProfile>("/api/account/me", timeout.Token))!;
        using var created = await TaskApiFactory.SendAsync(admin, HttpMethod.Post, "/api/tasks",
            new { title = "Live assignment", assigneeId = profile.Id });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var task = (await created.Content.ReadFromJsonAsync<TaskDetail>(TaskApiFactory.Json, timeout.Token))!;

        await using var assigneeConnection = await ConnectAsync(assignee, assigneeEmail);
        await using var outsiderConnection = await ConnectAsync(outsider, outsiderEmail);
        var assigneeUpdates = Subscribe(assigneeConnection);
        var outsiderUpdates = Subscribe(outsiderConnection);
        await Task.WhenAll(assigneeConnection.StartAsync(timeout.Token), outsiderConnection.StartAsync(timeout.Token));
        await assigneeUpdates.ReadAsync(timeout.Token);
        await outsiderUpdates.ReadAsync(timeout.Token);

        using var changed = await TaskApiFactory.SendAsync(admin, HttpMethod.Patch, $"/api/tasks/{task.Id}/status",
            new ChangeStatusInput(WorkStatus.Completed, task.Version));
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        await assigneeUpdates.ReadAsync(timeout.Token);
        var refreshed = await assignee.GetFromJsonAsync<TaskDetail>($"/api/tasks/{task.Id}", TaskApiFactory.Json, timeout.Token);
        Assert.Equal(WorkStatus.Completed, refreshed!.Status);
        using var noBroadcast = new CancellationTokenSource(TimeSpan.FromMilliseconds(400));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => { await outsiderUpdates.ReadAsync(noBroadcast.Token); });
    }

    private async Task<HubConnection> ConnectAsync(HttpClient client, string email)
    {
        using var login = await TaskApiFactory.SendAsync(client, HttpMethod.Post, "/api/account/login",
            new { email, password = "cohort-test-21840" });
        login.EnsureSuccessStatusCode();
        var cookie = login.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith("Taskline.Session=", StringComparison.Ordinal)).Split(';')[0];
        return new HubConnectionBuilder().WithUrl("https://localhost/api/task-updates", options =>
        {
            options.Transports = HttpTransportType.LongPolling;
            options.Headers["Cookie"] = cookie;
            options.HttpMessageHandlerFactory = _ => app.Server.CreateHandler();
        }).Build();
    }

    private static ChannelReader<bool> Subscribe(HubConnection connection)
    {
        var notifications = Channel.CreateUnbounded<bool>();
        connection.On("TasksChanged", () => { notifications.Writer.TryWrite(true); });
        return notifications.Reader;
    }
}
