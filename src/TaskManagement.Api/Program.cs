using System.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;
using TaskManagement.Api.Auth;
using TaskManagement.Api.Data;

var provisionAdmin = args.Contains("--seed-admin", StringComparer.Ordinal);
var builder = WebApplication.CreateBuilder(args.Where(arg => arg != "--seed-admin").ToArray());
builder.Services.AddSerilog((_, logger) => logger
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(new Serilog.Formatting.Json.JsonFormatter(renderMessage: true)));

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["traceId"] =
            Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
    };
});
builder.Services.AddControllersWithViews(options => options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Tasks")));

builder.Services.AddIdentity<AppUser, IdentityRole>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Password.RequiredLength = 10;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Lockout.MaxFailedAccessAttempts = 5;
})
    .AddEntityFrameworkStores<AppDbContext>();

var secureCookies = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "Taskline.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = secureCookies;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "Taskline.Csrf";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = secureCookies;
    options.Cookie.SameSite = SameSiteMode.Strict;
});
builder.Services.AddScoped<AccountService>();

await using var app = builder.Build();

if (provisionAdmin)
{
    var email = app.Configuration["AdminBootstrap:Email"];
    var password = app.Configuration["AdminBootstrap:Password"];
    var displayName = app.Configuration["AdminBootstrap:DisplayName"] ?? "Administrator";
    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
        throw new InvalidOperationException("Set AdminBootstrap:Email and AdminBootstrap:Password before provisioning.");

    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AccountService>()
        .ProvisionAdminAsync(email, password, displayName, CancellationToken.None);
    return;
}

app.UseSerilogRequestLogging();
app.UseExceptionHandler(new ExceptionHandlerOptions { SuppressDiagnosticsCallback = _ => false });
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/api/health", () => Results.Ok(new { status = "healthy" }));
await app.RunAsync();

public partial class Program;
