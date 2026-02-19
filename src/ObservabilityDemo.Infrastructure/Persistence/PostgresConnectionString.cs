namespace ObservabilityDemo.Infrastructure.Persistence;

public sealed class PostgresConnectionString(string value)
{
    public string Value { get; } = value;
}
