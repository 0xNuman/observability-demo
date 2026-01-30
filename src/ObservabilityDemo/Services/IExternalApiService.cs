namespace ObservabilityDemo.Services;

/// <summary>
/// Service interface for external API interactions.
/// Abstracts the external dependency for testability.
/// </summary>
public interface IExternalApiService
{
    Task<User?> GetUserAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Post>> GetPostsAsync(int limit, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a user from the JSONPlaceholder API.
/// Uses primary constructor (C# 12+) for concise definition.
/// </summary>
public record User(
    int Id,
    string Name,
    string Username,
    string Email,
    Address Address,
    string Phone,
    string Website,
    Company Company);

public record Address(
    string Street,
    string Suite,
    string City,
    string Zipcode,
    Geo Geo);

public record Geo(string Lat, string Lng);

public record Company(string Name, string CatchPhrase, string Bs);

/// <summary>
/// Represents a post from the JSONPlaceholder API.
/// </summary>
public record Post(int UserId, int Id, string Title, string Body);
