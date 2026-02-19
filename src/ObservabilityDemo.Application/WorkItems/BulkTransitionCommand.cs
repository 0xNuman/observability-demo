using ObservabilityDemo.Domain.Enums;

namespace ObservabilityDemo.Application.WorkItems;

public sealed record BulkTransitionCommand(
    IReadOnlyCollection<Guid> WorkItemIds,
    WorkItemStatus TargetStatus,
    string? ChangedBy,
    string? CorrelationId);
