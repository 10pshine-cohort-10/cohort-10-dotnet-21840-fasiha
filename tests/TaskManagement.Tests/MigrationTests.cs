using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using TaskManagement.Api.Auth;
using TaskManagement.Api.Data;
using TaskManagement.Api.Tasks;

namespace TaskManagement.Tests;

public sealed class MigrationTests(TaskApiFactory app) : IClassFixture<TaskApiFactory>
{
    [SqlServerFact]
    public async Task UpgradeKeepsRoleMembershipsAndExistingTasks()
    {
        const string email = "upgrade@example.test";
        using var previousSession = await app.SignInNewUserAsync(email, "Upgrade User");
        var previousProfile = (await previousSession.GetFromJsonAsync<AccountProfile>("/api/account/me"))!;
        using var created = await TaskApiFactory.SendAsync(previousSession, HttpMethod.Post, "/api/tasks",
            new { title = "Task from the old database" });
        created.EnsureSuccessStatusCode();
        var previousTask = (await created.Content.ReadFromJsonAsync<TaskDetail>(TaskApiFactory.Json))!;

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20261125204125_CreateTasks");
            Assert.Equal(new[] { "Admin", "Member" }, await db.Roles.OrderBy(role => role.Name).Select(role => role.Name).ToArrayAsync());
            await migrator.MigrateAsync();
            Assert.Equal(new[] { "Admin", "User" }, await db.Roles.OrderBy(role => role.Name).Select(role => role.Name).ToArrayAsync());
        }

        using var currentSession = app.CreateSession();
        await TaskApiFactory.LoginAsync(currentSession, email);
        var profile = (await currentSession.GetFromJsonAsync<AccountProfile>("/api/account/me"))!;
        Assert.Equal(previousProfile.Id, profile.Id);
        Assert.Equal(RoleNames.User, Assert.Single(profile.Roles));
        var task = await currentSession.GetFromJsonAsync<TaskDetail>("/api/tasks/" + previousTask.Id, TaskApiFactory.Json);
        Assert.Equal(previousTask.Title, task?.Title);
        Assert.Equal(profile.Id, task?.Creator.Id);
    }
}
