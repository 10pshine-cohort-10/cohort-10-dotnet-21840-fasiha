using System.Net;
using System.Net.Http.Json;
using TaskManagement.Api.Auth;

namespace TaskManagement.Tests;

public sealed class AccountEndpointsTests(TaskApiFactory app) : IClassFixture<TaskApiFactory>
{
    [Fact]
    public async Task RegistrationLoginProfileAndLogoutUseMemberSessionAndCsrf()
    {
        const string email = "member@example.test";
        using var client = app.CreateSession();
        var request = new { email, displayName = "Taskline Member", password = "cohort-test-21840", role = "Admin" };

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
        Assert.Equal("Taskline Member", profile.DisplayName);
        Assert.Equal(email, profile.Email);
        Assert.Equal(RoleNames.Member, Assert.Single(profile.Roles));

        using var directory = await client.GetAsync("/api/account/assignees");
        Assert.Equal(HttpStatusCode.Forbidden, directory.StatusCode);
        using var logout = await TaskApiFactory.SendAsync(client, HttpMethod.Post, "/api/account/logout");
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        using var afterLogout = await client.GetAsync("/api/account/me");
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }
}
