using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TaskManagement.Api.Auth;
using TaskManagement.Api.Data;

namespace TaskManagement.Tests;

public sealed class TaskApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string? sqlConnection = CreateSqlConnection();
    private readonly SqliteConnection sqlite = new("Data Source=:memory:");
    private bool databaseReady;

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options =>
            {
                if (sqlConnection is null)
                    options.UseSqlite(sqlite);
                else
                    options.UseSqlServer(sqlConnection);
            });
        });
    }

    public async Task InitializeAsync()
    {
        if (sqlConnection is null)
            await sqlite.OpenAsync();

        await using var scope = Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>().Database;
        if (sqlConnection is null)
            await database.EnsureCreatedAsync();
        else
            await database.MigrateAsync();

        databaseReady = true;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        try
        {
            if (databaseReady && sqlConnection is not null)
            {
                await using var scope = Services.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
            }
        }
        finally
        {
            await base.DisposeAsync();
            await sqlite.DisposeAsync();
        }
    }

    public HttpClient CreateSession() =>
        CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

    public async Task<HttpClient> SignInNewUserAsync(string email, string name)
    {
        var client = CreateSession();
        using var registration = await SendAsync(client, HttpMethod.Post, "/api/account/register",
            new { email, displayName = name, password = "cohort-test-21840" });
        registration.EnsureSuccessStatusCode();
        await LoginAsync(client, email);
        return client;
    }

    public async Task<HttpClient> SignInAdminAsync(string email)
    {
        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AccountService>()
            .ProvisionAdminAsync(email, "cohort-test-21840", "Test Administrator", CancellationToken.None);
        var client = CreateSession();
        await LoginAsync(client, email);
        return client;
    }

    public static async Task LoginAsync(HttpClient client, string email)
    {
        using var login = await SendAsync(client, HttpMethod.Post, "/api/account/login",
            new { email, password = "cohort-test-21840" });
        login.EnsureSuccessStatusCode();
    }

    public static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path, object? data = null)
    {
        var csrf = await client.GetFromJsonAsync<CsrfResponse>("/api/account/csrf");
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-CSRF-TOKEN", csrf!.Token);
        if (data is not null)
            request.Content = JsonContent.Create(data, options: Json);
        return await client.SendAsync(request);
    }

    private static string? CreateSqlConnection()
    {
        var value = Environment.GetEnvironmentVariable("TASKMANAGEMENT_TEST_SQL");
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return new SqlConnectionStringBuilder(value) { InitialCatalog = $"TasklineTest_{Guid.NewGuid():N}" }.ConnectionString;
    }

    private sealed record CsrfResponse(string Token);
}
