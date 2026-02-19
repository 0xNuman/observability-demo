using ObservabilityDemo.Application;
using ObservabilityDemo.Api.Observability;
using ObservabilityDemo.Infrastructure;
using ObservabilityDemo.Api.Tenancy;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

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

app.Run();

public partial class Program;
