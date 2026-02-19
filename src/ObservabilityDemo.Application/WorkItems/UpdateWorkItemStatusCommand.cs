using ObservabilityDemo.Domain.Enums;

namespace ObservabilityDemo.Application.WorkItems;

public sealed record UpdateWorkItemStatusCommand(
    WorkItemStatus TargetStatus,
    string? UpdatedBy);
