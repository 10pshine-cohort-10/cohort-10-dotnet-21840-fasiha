using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Serilog;
using TaskManagement.Api.Data;

var builder = WebApplication.CreateBuilder(args);
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
builder.Services.AddControllers();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Tasks")));

var app = builder.Build();
app.UseSerilogRequestLogging();
app.UseExceptionHandler(new ExceptionHandlerOptions { SuppressDiagnosticsCallback = _ => false });
app.UseStatusCodePages();
app.MapControllers();
app.MapGet("/api/health", () => Results.Ok(new { status = "healthy" }));
await app.RunAsync();

public partial class Program;
