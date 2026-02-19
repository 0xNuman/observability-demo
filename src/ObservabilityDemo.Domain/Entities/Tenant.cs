namespace ObservabilityDemo.Domain.Entities;

public sealed class Tenant
{
    public Guid Id { get; init; }

    public string Name { get; private set; } = string.Empty;

    public bool IsActive { get; private set; } = true;

    public DateTimeOffset CreatedAtUtc { get; init; }

    public static Tenant Create(Guid id, string name, DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Tenant id is required.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Tenant name is required.", nameof(name));
        }

        return new Tenant
        {
            Id = id,
            Name = name.Trim(),
            CreatedAtUtc = createdAtUtc,
        };
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}
