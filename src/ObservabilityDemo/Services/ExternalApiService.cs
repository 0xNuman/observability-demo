using System.Diagnostics;
using System.Net.Http.Json;
using ObservabilityDemo.Telemetry;
using OpenTelemetry.Trace;

namespace ObservabilityDemo.Services;

/// <summary>
/// Implementation of external API service using HttpClient.
/// HttpClient is automatically instrumented by OpenTelemetry.
/// </summary>
public sealed class ExternalApiService(
    HttpClient httpClient,
    DemoTelemetry telemetry,
    ILogger<ExternalApiService> logger) : IExternalApiService
{
    private const string ApiName = "jsonplaceholder";

    public async Task<User?> GetUserAsync(int id, CancellationToken cancellationToken = default)
    {
        // Create a custom span for business context
        using var activity = telemetry.StartActivity("GetUser");
        activity?.SetTag("user.id", id);

        logger.LogInformation("Fetching user {UserId} from external API", id);

        try
        {
            var user = await httpClient.GetFromJsonAsync<User>(
                $"users/{id}",
                cancellationToken);

            if (user is not null)
            {
                activity?.SetTag("user.name", user.Name);
                activity?.SetTag("user.email", user.Email);
                logger.LogInformation(
                    "Successfully retrieved user {UserId}: {UserName}",
                    id, user.Name);
            }
            else
            {
                logger.LogWarning("User {UserId} not found", id);
            }

            telemetry.RecordExternalApiCall(ApiName, "users", success: true);
            return user;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.RecordException(ex);
            telemetry.RecordExternalApiCall(ApiName, "users", success: false);

            logger.LogError(ex, "Failed to fetch user {UserId} from external API", id);
            throw;
        }
    }

    public async Task<IReadOnlyList<Post>> GetPostsAsync(int limit, CancellationToken cancellationToken = default)
    {
        using var activity = telemetry.StartActivity("GetPosts");
        activity?.SetTag("posts.limit", limit);

        logger.LogInformation("Fetching up to {Limit} posts from external API", limit);

        try
        {
            var posts = await httpClient.GetFromJsonAsync<List<Post>>(
                "posts",
                cancellationToken) ?? [];

            // Apply limit
            var limitedPosts = posts.Take(limit).ToList();

            activity?.SetTag("posts.count", limitedPosts.Count);
            logger.LogInformation(
                "Successfully retrieved {Count} posts (limit was {Limit})",
                limitedPosts.Count, limit);

            telemetry.RecordExternalApiCall(ApiName, "posts", success: true);
            return limitedPosts;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.RecordException(ex);
            telemetry.RecordExternalApiCall(ApiName, "posts", success: false);

            logger.LogError(ex, "Failed to fetch posts from external API");
            throw;
        }
    }
}
