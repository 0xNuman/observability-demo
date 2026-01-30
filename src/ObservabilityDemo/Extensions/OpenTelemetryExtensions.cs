using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace ObservabilityDemo.Extensions;

/// <summary>
/// OpenTelemetry configuration extensions - similar to Aspire's ServiceDefaults.
///
/// This consolidates ALL OpenTelemetry setup into a single extension method,
/// following the same pattern that Aspire uses internally.
///
/// NO custom metrics or spans needed! Automatic instrumentation handles:
/// - HTTP server requests (ASP.NET Core)
/// - HTTP client requests (HttpClient)
/// - Runtime metrics (.NET runtime)
/// </summary>
public static class OpenTelemetryExtensions
{
    /// <summary>
    /// Adds OpenTelemetry observability to the application.
    /// This is equivalent to what Aspire's AddServiceDefaults() does for telemetry.
    ///
    /// Usage: builder.AddObservability();
    /// </summary>
    public static IHostApplicationBuilder AddObservability(this IHostApplicationBuilder builder)
    {
        // Get configuration
        var serviceName = builder.Configuration["OTEL_SERVICE_NAME"] ?? "observability-demo";
        var serviceVersion = typeof(OpenTelemetryExtensions).Assembly.GetName().Version?.ToString() ?? "1.0.0";
        var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] ?? "http://localhost:4317";

        // Configure OpenTelemetry
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName: serviceName, serviceVersion: serviceVersion)
                .AddAttributes([
                    new("deployment.environment", builder.Environment.EnvironmentName),
                    new("host.name", Environment.MachineName)
                ]))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options =>
                {
                    options.RecordException = true;
                })
                .AddHttpClientInstrumentation(options =>
                {
                    options.RecordException = true;
                })
                .AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint)))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .SetExemplarFilter(ExemplarFilterType.TraceBased)
                .AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint)));

        // Configure logging to export via OTLP
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
            logging.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
        });

        return builder;
    }
}
