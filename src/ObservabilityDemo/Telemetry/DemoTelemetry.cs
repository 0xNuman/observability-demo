using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ObservabilityDemo.Telemetry;

/// <summary>
/// Central telemetry class for custom metrics and activities.
/// Follows OpenTelemetry best practices for .NET instrumentation.
/// </summary>
public sealed class DemoTelemetry : IDisposable
{
    public const string ActivitySourceName = "ObservabilityDemo";
    public const string MeterName = "ObservabilityDemo";

    // Activity source for custom spans
    public ActivitySource ActivitySource { get; }

    // Meter for custom metrics
    private readonly Meter _meter;

    // Custom metrics
    private readonly Counter<long> _requestCounter;
    private readonly Histogram<double> _requestDuration;
    private readonly Counter<long> _externalApiCallsCounter;

    public DemoTelemetry()
    {
        ActivitySource = new ActivitySource(ActivitySourceName, "1.0.0");
        _meter = new Meter(MeterName, "1.0.0");

        // Initialize custom metrics
        _requestCounter = _meter.CreateCounter<long>(
            "demo.requests.total",
            unit: "{request}",
            description: "Total number of requests processed by endpoint and status");

        _requestDuration = _meter.CreateHistogram<double>(
            "demo.request.duration",
            unit: "ms",
            description: "Duration of requests in milliseconds by endpoint and status");

        _externalApiCallsCounter = _meter.CreateCounter<long>(
            "demo.external_api_calls.total",
            unit: "{call}",
            description: "Total number of external API calls by API and endpoint");
    }

    /// <summary>
    /// Records a request with its duration and status.
    /// Tags enable filtering and aggregation in queries.
    /// </summary>
    public void RecordRequest(string endpoint, int statusCode, double durationMs)
    {
        var tags = new TagList
        {
            { "endpoint", endpoint },
            { "status_code", statusCode.ToString() },
            { "status_class", GetStatusClass(statusCode) }
        };

        _requestCounter.Add(1, tags);
        _requestDuration.Record(durationMs, tags);
    }

    /// <summary>
    /// Records an external API call for tracking third-party dependencies.
    /// </summary>
    public void RecordExternalApiCall(string api, string endpoint, bool success)
    {
        var tags = new TagList
        {
            { "api", api },
            { "endpoint", endpoint },
            { "success", success.ToString().ToLowerInvariant() }
        };

        _externalApiCallsCounter.Add(1, tags);
    }

    /// <summary>
    /// Starts a new activity (span) for custom business operations.
    /// </summary>
    public Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal)
    {
        return ActivitySource.StartActivity(name, kind);
    }

    /// <summary>
    /// Converts HTTP status code to a category for aggregation.
    /// </summary>
    private static string GetStatusClass(int statusCode) => statusCode switch
    {
        >= 200 and < 300 => "2xx",
        >= 300 and < 400 => "3xx",
        >= 400 and < 500 => "4xx",
        >= 500 => "5xx",
        _ => "unknown"
    };

    public void Dispose()
    {
        ActivitySource.Dispose();
        _meter.Dispose();
    }
}
