using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using ObservabilityDemo.Endpoints;
using ObservabilityDemo.Services;
using ObservabilityDemo.Telemetry;

var builder = WebApplication.CreateBuilder(args);

// Configure service name for telemetry
var serviceName = "observability-demo";
var serviceVersion = "1.0.0";

// Build resource with service information
var resourceBuilder = ResourceBuilder.CreateDefault()
    .AddService(serviceName: serviceName, serviceVersion: serviceVersion)
    .AddAttributes(
    [
        new KeyValuePair<string, object>("deployment.environment", builder.Environment.EnvironmentName),
        new KeyValuePair<string, object>("host.name", Environment.MachineName)
    ]);

// Get OTLP endpoint from configuration
var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] ?? "http://localhost:4317";

// Configure OpenTelemetry Tracing
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName: serviceName, serviceVersion: serviceVersion)
        .AddAttributes(
        [
            new KeyValuePair<string, object>("deployment.environment", builder.Environment.EnvironmentName),
            new KeyValuePair<string, object>("host.name", Environment.MachineName)
        ]))
    .WithTracing(tracing => tracing
        // Automatic instrumentation
        .AddAspNetCoreInstrumentation(options =>
        {
            options.RecordException = true;
            options.EnrichWithHttpRequest = (activity, request) =>
            {
                activity.SetTag("http.request.header.user_agent", request.Headers.UserAgent.ToString());
            };
            options.EnrichWithHttpResponse = (activity, response) =>
            {
                activity.SetTag("http.response.content_length", response.ContentLength);
            };
        })
        .AddHttpClientInstrumentation(options =>
        {
            options.RecordException = true;
            options.EnrichWithHttpRequestMessage = (activity, request) =>
            {
                activity.SetTag("http.request.uri", request.RequestUri?.ToString());
            };
            options.EnrichWithHttpResponseMessage = (activity, response) =>
            {
                activity.SetTag("http.response.status_code", (int)response.StatusCode);
            };
        })
        // Custom activity source for business spans
        .AddSource(DemoTelemetry.ActivitySourceName)
        // OTLP exporter to collector
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri(otlpEndpoint);
        }))
    .WithMetrics(metrics => metrics
        // Automatic instrumentation
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        // Custom business metrics
        .AddMeter(DemoTelemetry.MeterName)
        // Configure exemplar sampling (important for linking metrics to traces)
        .SetExemplarFilter(ExemplarFilterType.TraceBased)
        // OTLP exporter to collector
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri(otlpEndpoint);
        }));

// Configure OpenTelemetry Logging
builder.Logging.ClearProviders();
builder.Logging.AddOpenTelemetry(logging =>
{
    logging.SetResourceBuilder(resourceBuilder);
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
    logging.ParseStateValues = true;
    logging.AddOtlpExporter(options =>
    {
        options.Endpoint = new Uri(otlpEndpoint);
    });
});

// Add console logging for local debugging
builder.Logging.AddConsole();

// Register services
builder.Services.AddHttpClient<IExternalApiService, ExternalApiService>(client =>
{
    client.BaseAddress = new Uri("https://jsonplaceholder.typicode.com/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Register telemetry service
builder.Services.AddSingleton<DemoTelemetry>();

// Add health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// Map endpoints
app.MapDemoEndpoints();
app.MapHealthChecks("/health");

// Add simple root endpoint
app.MapGet("/", () => Results.Ok(new
{
    service = serviceName,
    version = serviceVersion,
    endpoints = new[]
    {
        "GET /api/demo/hello - Simple hello endpoint",
        "GET /api/demo/users/{id} - Get user from external API",
        "GET /api/demo/posts?limit={n} - Get posts from external API",
        "GET /api/demo/slow?delayMs={n} - Configurable delay endpoint",
        "GET /api/demo/error - Intentional error endpoint",
        "GET /health - Health check"
    }
}));

app.Run();
