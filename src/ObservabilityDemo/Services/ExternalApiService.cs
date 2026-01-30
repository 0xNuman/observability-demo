using System.Net.Http.Json;

namespace ObservabilityDemo.Services;

/// <summary>
/// External API service - CLEAN version with ZERO telemetry code.
///
/// HttpClient is AUTOMATICALLY instrumented by OpenTelemetry!
/// You get for FREE:
/// - http.client.request.duration histogram
/// - Distributed trace propagation (trace context headers)
/// - Error recording
/// - Request/response size metrics
///
/// No manual instrumentation needed!
/// </summary>
public sealed class ExternalApiService(
    HttpClient httpClient,
    ILogger<ExternalApiService> logger) : IExternalApiService
{
    public async Task<User?> GetUserAsync(int id, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Fetching user {UserId} from external API", id);

        var user = await httpClient.GetFromJsonAsync<User>(
            $"users/{id}",
            cancellationToken);

        if (user is not null)
        {
            logger.LogInformation("Retrieved user {UserId}: {UserName}", id, user.Name);
        }
        else
        {
            logger.LogWarning("User {UserId} not found", id);
        }

        return user;
    }

    public async Task<IReadOnlyList<Post>> GetPostsAsync(int limit, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Fetching posts with limit {Limit}", limit);

        var posts = await httpClient.GetFromJsonAsync<List<Post>>(
            "posts",
            cancellationToken) ?? [];

        var limitedPosts = posts.Take(limit).ToList();

        logger.LogInformation("Retrieved {Count} posts", limitedPosts.Count);

        return limitedPosts;
    }
}
