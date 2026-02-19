using ObservabilityDemo.Domain.Enums;

namespace ObservabilityDemo.Api.Contracts.WorkItems;

public sealed class BulkTransitionRequest
{
    public IReadOnlyCollection<Guid> WorkItemIds { get; init; } = Array.Empty<Guid>();

    public WorkItemStatus TargetStatus { get; init; }

    public string? ChangedBy { get; init; }

    public string? CorrelationId { get; init; }
}
