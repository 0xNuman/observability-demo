using System.Diagnostics;
using ObservabilityDemo.Services;
using ObservabilityDemo.Telemetry;
using OpenTelemetry.Trace;

namespace ObservabilityDemo.Endpoints;

/// <summary>
/// Demo API endpoints showcasing different observability scenarios.
/// Uses minimal API pattern with endpoint grouping.
/// </summary>
public static class DemoEndpoints
{
    public static void MapDemoEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/demo")
            .WithTags("Demo");

        // Simple fast endpoint for baseline metrics
        group.MapGet("/hello", HandleHello)
            .WithName("Hello")
            .WithSummary("Simple hello endpoint for baseline metrics");

        // External API call - demonstrates distributed tracing
        group.MapGet("/users/{id:int}", HandleGetUser)
            .WithName("GetUser")
            .WithSummary("Fetches user from external JSONPlaceholder API");

        // Another external API call with query parameter
        group.MapGet("/posts", HandleGetPosts)
            .WithName("GetPosts")
            .WithSummary("Fetches posts from external JSONPlaceholder API");

        // Configurable delay endpoint - demonstrates latency percentiles
        group.MapGet("/slow", HandleSlow)
            .WithName("Slow")
            .WithSummary("Configurable delay endpoint for latency testing");

        // Intentional error endpoint - demonstrates error tracking
        group.MapGet("/error", HandleError)
            .WithName("Error")
            .WithSummary("Intentional error endpoint for error rate testing");
    }

    /// <summary>
    /// Simple hello endpoint. Fast response for baseline metrics.
    /// </summary>
    private static IResult HandleHello(
        DemoTelemetry telemetry,
        ILogger<Program> logger)
    {
        var stopwatch = Stopwatch.StartNew();

        logger.LogInformation("Processing hello request");

        var response = new
        {
            message = "Hello from Observability Demo!",
            timestamp = DateTime.UtcNow,
            traceId = Activity.Current?.TraceId.ToString()
        };

        stopwatch.Stop();
        telemetry.RecordRequest("/api/demo/hello", 200, stopwatch.Elapsed.TotalMilliseconds);

        return Results.Ok(response);
    }

    /// <summary>
    /// Fetches a user from external API.
    /// Demonstrates distributed tracing with HttpClient instrumentation.
    /// </summary>
    private static async Task<IResult> HandleGetUser(
        int id,
        IExternalApiService externalApi,
        DemoTelemetry telemetry,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        logger.LogInformation("Handling get user request for ID {UserId}", id);

        try
        {
            var user = await externalApi.GetUserAsync(id, cancellationToken);

            stopwatch.Stop();
            var statusCode = user is null ? 404 : 200;
            telemetry.RecordRequest("/api/demo/users/{id}", statusCode, stopwatch.Elapsed.TotalMilliseconds);

            if (user is null)
            {
                logger.LogWarning("User {UserId} not found", id);
                return Results.NotFound(new { error = $"User {id} not found" });
            }

            return Results.Ok(new
            {
                user,
                traceId = Activity.Current?.TraceId.ToString()
            });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            telemetry.RecordRequest("/api/demo/users/{id}", 500, stopwatch.Elapsed.TotalMilliseconds);

            logger.LogError(ex, "Error fetching user {UserId}", id);
            return Results.Problem(
                detail: ex.Message,
                statusCode: 500,
                title: "External API Error");
        }
    }

    /// <summary>
    /// Fetches posts from external API with configurable limit.
    /// </summary>
    private static async Task<IResult> HandleGetPosts(
        IExternalApiService externalApi,
        DemoTelemetry telemetry,
        ILogger<Program> logger,
        CancellationToken cancellationToken,
        int limit = 10)
    {
        var stopwatch = Stopwatch.StartNew();

        logger.LogInformation("Handling get posts request with limit {Limit}", limit);

        try
        {
            var posts = await externalApi.GetPostsAsync(limit, cancellationToken);

            stopwatch.Stop();
            telemetry.RecordRequest("/api/demo/posts", 200, stopwatch.Elapsed.TotalMilliseconds);

            return Results.Ok(new
            {
                count = posts.Count,
                posts,
                traceId = Activity.Current?.TraceId.ToString()
            });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            telemetry.RecordRequest("/api/demo/posts", 500, stopwatch.Elapsed.TotalMilliseconds);

            logger.LogError(ex, "Error fetching posts");
            return Results.Problem(
                detail: ex.Message,
                statusCode: 500,
                title: "External API Error");
        }
    }

    /// <summary>
    /// Configurable delay endpoint for testing latency percentiles.
    /// Useful for demonstrating p50, p95, p99 distributions.
    /// </summary>
    private static async Task<IResult> HandleSlow(
        DemoTelemetry telemetry,
        ILogger<Program> logger,
        CancellationToken cancellationToken,
        int delayMs = 100)
    {
        var stopwatch = Stopwatch.StartNew();

        // Clamp delay to reasonable bounds
        var actualDelay = Math.Clamp(delayMs, 0, 10000);

        logger.LogInformation("Processing slow request with {DelayMs}ms delay", actualDelay);

        // Create a custom span for the delay operation
        using (var activity = Activity.Current?.Source.StartActivity("SimulatedDelay"))
        {
            activity?.SetTag("delay.requested_ms", delayMs);
            activity?.SetTag("delay.actual_ms", actualDelay);

            await Task.Delay(actualDelay, cancellationToken);
        }

        stopwatch.Stop();
        telemetry.RecordRequest("/api/demo/slow", 200, stopwatch.Elapsed.TotalMilliseconds);

        return Results.Ok(new
        {
            message = "Slow operation completed",
            requestedDelayMs = delayMs,
            actualDelayMs = actualDelay,
            actualDurationMs = stopwatch.Elapsed.TotalMilliseconds,
            traceId = Activity.Current?.TraceId.ToString()
        });
    }

    /// <summary>
    /// Intentional error endpoint for testing error tracking.
    /// </summary>
    private static IResult HandleError(
        DemoTelemetry telemetry,
        ILogger<Program> logger)
    {
        var stopwatch = Stopwatch.StartNew();

        logger.LogWarning("Intentional error endpoint called");

        // Record exception in current activity
        var exception = new InvalidOperationException("This is an intentional error for demonstration purposes");

        Activity.Current?.SetStatus(ActivityStatusCode.Error, exception.Message);
        Activity.Current?.RecordException(exception);

        logger.LogError(exception, "Intentional error triggered");

        stopwatch.Stop();
        telemetry.RecordRequest("/api/demo/error", 500, stopwatch.Elapsed.TotalMilliseconds);

        return Results.Problem(
            detail: exception.Message,
            statusCode: 500,
            title: "Intentional Error",
            extensions: new Dictionary<string, object?>
            {
                ["traceId"] = Activity.Current?.TraceId.ToString()
            });
    }
}
