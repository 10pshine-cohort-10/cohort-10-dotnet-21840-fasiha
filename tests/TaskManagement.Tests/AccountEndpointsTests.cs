using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskManagement.Api.Auth;
using TaskManagement.Api.Data;
using TaskManagement.Api.Tasks;

namespace TaskManagement.Tests;

public sealed class AccountEndpointsTests(TaskApiFactory app) : IClassFixture<TaskApiFactory>
{
    [SqlServerFact]
    public async Task RegistrationLoginProfileAndLogoutUseUserSessionAndCsrf()
    {
        const string email = "member@example.test";
        using var client = app.CreateSession();
        var request = new { email, displayName = "Taskline User", password = "cohort-test-21840", role = "Admin" };

        using var anonymousProfile = await client.GetAsync("/api/account/me");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousProfile.StatusCode);

        using var missingCsrf = await client.PostAsJsonAsync("/api/account/register", request);
        Assert.Equal(HttpStatusCode.BadRequest, missingCsrf.StatusCode);

        using var registration = await TaskApiFactory.SendAsync(client, HttpMethod.Post, "/api/account/register", request);
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        using var duplicate = await TaskApiFactory.SendAsync(client, HttpMethod.Post, "/api/account/register", request);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        using var badPassword = await TaskApiFactory.SendAsync(client, HttpMethod.Post, "/api/account/login", new { email, password = "wrong" });
        Assert.Equal(HttpStatusCode.Unauthorized, badPassword.StatusCode);
        await TaskApiFactory.LoginAsync(client, email);

        var profile = await client.GetFromJsonAsync<AccountProfile>("/api/account/me");
        Assert.NotNull(profile);
        Assert.Equal("Taskline User", profile.DisplayName);
        Assert.Equal(email, profile.Email);
        Assert.Equal(RoleNames.User, Assert.Single(profile.Roles));

        using var directory = await client.GetAsync("/api/account/assignees");
        Assert.Equal(HttpStatusCode.Forbidden, directory.StatusCode);
        using var logout = await TaskApiFactory.SendAsync(client, HttpMethod.Post, "/api/account/logout");
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        using var afterLogout = await client.GetAsync("/api/account/me");
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }

    [SqlServerFact]
    public async Task AdminSoftDeletesUserAndRevokesTheirSessionWithoutHidingOwnedTasks()
    {
        const string email = "deactivate@example.test";
        using var user = await app.SignInNewUserAsync(email, "Deactivated User");
        using var admin = await app.SignInAdminAsync("deletion-admin@example.test");
        var profile = (await user.GetFromJsonAsync<AccountProfile>("/api/account/me"))!;
        var adminProfile = (await admin.GetFromJsonAsync<AccountProfile>("/api/account/me"))!;
        var path = "/api/account/users/" + profile.Id;

        using var unauthorized = await TaskApiFactory.SendAsync(user, HttpMethod.Delete, path);
        Assert.Equal(HttpStatusCode.Forbidden, unauthorized.StatusCode);
        using var protectedAdmin = await TaskApiFactory.SendAsync(admin, HttpMethod.Delete, "/api/account/users/" + adminProfile.Id);
        Assert.Equal(HttpStatusCode.Conflict, protectedAdmin.StatusCode);
        using var created = await TaskApiFactory.SendAsync(user, HttpMethod.Post, "/api/tasks", new { title = "Retained task" });
        created.EnsureSuccessStatusCode();
        var task = (await created.Content.ReadFromJsonAsync<TaskDetail>(TaskApiFactory.Json))!;

        using var deleted = await TaskApiFactory.SendAsync(admin, HttpMethod.Delete, path);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        using var profileAfterDeletion = await user.GetAsync("/api/account/me");
        Assert.Equal(HttpStatusCode.Unauthorized, profileAfterDeletion.StatusCode);
        using var login = await TaskApiFactory.SendAsync(user, HttpMethod.Post, "/api/account/login",
            new { email, password = "cohort-test-21840" });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        using var registration = await TaskApiFactory.SendAsync(user, HttpMethod.Post, "/api/account/register",
            new { email, displayName = "Duplicate User", password = "cohort-test-21840" });
        Assert.Equal(HttpStatusCode.Conflict, registration.StatusCode);

        var options = await admin.GetFromJsonAsync<AssigneeOption[]>("/api/account/assignees?search=deactivate");
        Assert.Empty(options!);
        using var blockedAssignment = await TaskApiFactory.SendAsync(admin, HttpMethod.Post, "/api/tasks",
            new { title = "Blocked assignment", assigneeId = profile.Id });
        Assert.Equal(HttpStatusCode.BadRequest, blockedAssignment.StatusCode);
        var retainedTask = await admin.GetFromJsonAsync<TaskDetail>("/api/tasks/" + task.Id, TaskApiFactory.Json);
        Assert.Equal(profile.Id, retainedTask?.Creator.Id);

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var account = await db.Users.IgnoreQueryFilters().SingleAsync(item => item.Id == profile.Id);
        Assert.True(account.IsDeleted);
    }
}
