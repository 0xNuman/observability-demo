using ObservabilityDemo.Domain.Enums;

namespace ObservabilityDemo.Api.Contracts.WorkItems;

public sealed class CreateWorkItemRequest
{
    public string Title { get; init; } = string.Empty;

    public string? Description { get; init; }

    public WorkItemPriority Priority { get; init; } = WorkItemPriority.Medium;

    public string? RequestedBy { get; init; }
}
