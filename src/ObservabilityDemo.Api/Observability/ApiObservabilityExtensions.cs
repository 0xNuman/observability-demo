using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using ObservabilityDemo.Api.Tenancy;
using ObservabilityDemo.Infrastructure.Observability;
using Serilog;
using Serilog.Formatting.Compact;
using Serilog.Sinks.OpenTelemetry;
using System.Diagnostics;

namespace ObservabilityDemo.Api.Observability;

public static class ApiObservabilityExtensions
{
    public static WebApplicationBuilder AddApiObservability(this WebApplicationBuilder builder)
    {
        var serviceName = builder.Configuration["Service:Name"] ?? "observability-demo-api";
        var configuredInstanceId = builder.Configuration["Service:InstanceId"];
        var serviceInstanceId = string.IsNullOrWhiteSpace(configuredInstanceId)
            ? Environment.MachineName
            : configuredInstanceId;
        var deploymentEnvironment = builder.Environment.EnvironmentName;
        var serviceVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0";
        var otlpEndpoint = builder.Configuration["Observability:OtlpEndpoint"] ?? "http://localhost:4317";
        var logsEndpoint = BuildSignalEndpoint(otlpEndpoint, "v1/logs");
        var tracesEndpoint = BuildSignalEndpoint(otlpEndpoint, "v1/traces");
        var metricsEndpoint = BuildSignalEndpoint(otlpEndpoint, "v1/metrics");

        builder.Host.UseSerilog(
            (_, _, loggerConfiguration) =>
            {
                loggerConfiguration
                    .Enrich.FromLogContext()
                    .Enrich.WithProperty("service_name", serviceName)
                    .Enrich.WithProperty("service_version", serviceVersion)
                    .Enrich.WithProperty("instance_id", serviceInstanceId)
                    .Enrich.WithProperty("deployment_environment", deploymentEnvironment)
                    .WriteTo.Console(new RenderedCompactJsonFormatter())
                    .WriteTo.OpenTelemetry(options =>
                    {
                        options.Endpoint = logsEndpoint;
                        options.Protocol = OtlpProtocol.HttpProtobuf;
                        options.ResourceAttributes = new Dictionary<string, object>
                        {
                            ["service.name"] = serviceName,
                            ["service.version"] = serviceVersion,
                            ["service.instance.id"] = serviceInstanceId,
                            ["deployment.environment"] = deploymentEnvironment,
                        };
                    });
            });

        // We attach stable resource attributes so traces/metrics/logs stay attributable
        // to a specific service instance when the API is horizontally scaled.
        builder.Services
            .AddOpenTelemetry()
            .ConfigureResource(resourceBuilder =>
            {
                resourceBuilder
                    .AddService(
                        serviceName: serviceName,
                        serviceVersion: serviceVersion,
                        serviceInstanceId: serviceInstanceId)
                    .AddAttributes(
                    [
                        new KeyValuePair<string, object>("deployment.environment", deploymentEnvironment),
                    ]);
            })
            .WithTracing(traceBuilder =>
            {
                // ASP.NET, HttpClient, and repository-level spans provide the full
                // request -> dependency path required for RCA and latency drill-down.
                traceBuilder
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
                    })
                    .AddHttpClientInstrumentation()
                    .AddSource("Npgsql")
                    .AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(tracesEndpoint);
                        options.Protocol = OtlpExportProtocol.HttpProtobuf;
                    });
            })
            .WithMetrics(metricsBuilder =>
            {
                // Explicit HTTP duration buckets are configured for reliable p50/p95/p99
                // dashboard queries without relying on exporter defaults.
                metricsBuilder
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter(WorkItemTelemetry.MeterName)
                    .AddView(
                        instrumentName: "http.server.request.duration",
                        new ExplicitBucketHistogramConfiguration
                        {
                            Boundaries =
                            [
                                0.005,
                                0.01,
                                0.025,
                                0.05,
                                0.1,
                                0.25,
                                0.5,
                                1,
                                2.5,
                                5,
                                10,
                            ],
                        })
                    .AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(metricsEndpoint);
                        options.Protocol = OtlpExportProtocol.HttpProtobuf;
                    });
            });

        return builder;
    }

    private static string BuildSignalEndpoint(string baseEndpoint, string signalPath)
    {
        return $"{baseEndpoint.TrimEnd('/')}/{signalPath}";
    }

    public static WebApplication UseApiObservability(this WebApplication app)
    {
        app.UseSerilogRequestLogging(options =>
        {
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                var activity = Activity.Current;
                if (activity is not null)
                {
                    diagnosticContext.Set("trace_id", activity.TraceId.ToString());
                    diagnosticContext.Set("span_id", activity.SpanId.ToString());
                }

                diagnosticContext.Set("request_path", httpContext.Request.Path.Value ?? string.Empty);
                diagnosticContext.Set("http_status_code", httpContext.Response.StatusCode);
                diagnosticContext.Set(
                    "tenant_id",
                    httpContext.Items.TryGetValue(TenantContextMiddleware.TenantLogPropertyName, out var tenantId)
                        ? tenantId?.ToString() ?? "unknown"
                        : "unknown");
                var configuredInstanceId = app.Configuration["Service:InstanceId"];
                diagnosticContext.Set(
                    "instance_id",
                    string.IsNullOrWhiteSpace(configuredInstanceId) ? Environment.MachineName : configuredInstanceId);
            };
        });

        return app;
    }
}
