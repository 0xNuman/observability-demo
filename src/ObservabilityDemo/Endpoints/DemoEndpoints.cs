using System.Diagnostics;
using ObservabilityDemo.Services;

namespace ObservabilityDemo.Endpoints;

/// <summary>
/// Demo API endpoints - CLEAN version without manual telemetry code.
///
/// Notice: NO manual stopwatches, NO manual metric recording!
/// OpenTelemetry's automatic instrumentation handles:
/// - Request duration (http.server.request.duration)
/// - Request count
/// - Status codes
/// - Exception recording
///
/// You only need manual instrumentation for BUSINESS metrics
/// (e.g., "orders_placed", "payments_processed").
/// </summary>
public static class DemoEndpoints
{
    public static void MapDemoEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/demo")
            .WithTags("Demo");

        group.MapGet("/hello", HandleHello);
        group.MapGet("/users/{id:int}", HandleGetUser);
        group.MapGet("/posts", HandleGetPosts);
        group.MapGet("/slow", HandleSlow);
        group.MapGet("/error", HandleError);
    }

    /// <summary>
    /// Simple hello endpoint - just business logic, no telemetry boilerplate.
    /// </summary>
    private static IResult HandleHello(ILogger<Program> logger)
    {
        logger.LogInformation("Processing hello request");

        return Results.Ok(new
        {
            message = "Hello from Observability Demo!",
            timestamp = DateTime.UtcNow,
            traceId = Activity.Current?.TraceId.ToString()
        });
    }

    /// <summary>
    /// Fetches a user from external API.
    /// HttpClient is automatically instrumented - no manual spans needed!
    /// </summary>
    private static async Task<IResult> HandleGetUser(
        int id,
        IExternalApiService externalApi,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Fetching user {UserId}", id);

        var user = await externalApi.GetUserAsync(id, cancellationToken);

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

    /// <summary>
    /// Fetches posts from external API.
    /// </summary>
    private static async Task<IResult> HandleGetPosts(
        IExternalApiService externalApi,
        ILogger<Program> logger,
        CancellationToken cancellationToken,
        int limit = 10)
    {
        logger.LogInformation("Fetching posts with limit {Limit}", limit);

        var posts = await externalApi.GetPostsAsync(limit, cancellationToken);

        return Results.Ok(new
        {
            count = posts.Count,
            posts,
            traceId = Activity.Current?.TraceId.ToString()
        });
    }

    /// <summary>
    /// Configurable delay endpoint for testing latency percentiles.
    /// </summary>
    private static async Task<IResult> HandleSlow(
        ILogger<Program> logger,
        CancellationToken cancellationToken,
        int delayMs = 100)
    {
        var actualDelay = Math.Clamp(delayMs, 0, 10000);

        logger.LogInformation("Processing slow request with {DelayMs}ms delay", actualDelay);

        await Task.Delay(actualDelay, cancellationToken);

        return Results.Ok(new
        {
            message = "Slow operation completed",
            requestedDelayMs = delayMs,
            actualDelayMs = actualDelay,
            traceId = Activity.Current?.TraceId.ToString()
        });
    }

    /// <summary>
    /// Intentional error endpoint for testing error tracking.
    /// </summary>
    private static IResult HandleError(ILogger<Program> logger)
    {
        logger.LogWarning("Intentional error endpoint called");

        // Just throw - OpenTelemetry will record the exception automatically
        throw new InvalidOperationException("This is an intentional error for demonstration purposes");
    }
}
