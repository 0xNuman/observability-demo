using ObservabilityDemo.Application;
using ObservabilityDemo.Api.Observability;
using ObservabilityDemo.Infrastructure;
using ObservabilityDemo.Api.Tenancy;
using System.Globalization;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
var processStartedAtUtc = DateTimeOffset.UtcNow;

builder.Services.AddProblemDetails();
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddHttpClient();
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.AddApiObservability();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseApiObservability();
app.UseMiddleware<TenantContextMiddleware>();

app.MapGet(
    "/",
    () => Results.Ok(
        new
        {
            service = "ObservabilityDemo.Api",
            status = "ok",
            utcNow = DateTimeOffset.UtcNow,
        }));

app.MapControllers();
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");
app.MapGet(
    "/health/prometheus",
    () =>
    {
        var uptimeSeconds = Math.Max(
            0,
            (DateTimeOffset.UtcNow - processStartedAtUtc).TotalSeconds);

        var payload = string.Create(
            CultureInfo.InvariantCulture,
            $$"""
# HELP api_up Whether this API process is up (1=up).
# TYPE api_up gauge
api_up 1
# HELP api_process_uptime_seconds API process uptime in seconds.
# TYPE api_process_uptime_seconds gauge
api_process_uptime_seconds {{uptimeSeconds:F0}}
""");

        return Results.Text(payload, "text/plain; version=0.0.4; charset=utf-8");
    });

app.Run();

public partial class Program;
