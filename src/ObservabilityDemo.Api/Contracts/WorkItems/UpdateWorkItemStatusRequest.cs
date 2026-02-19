using ObservabilityDemo.Domain.Enums;

namespace ObservabilityDemo.Api.Contracts.WorkItems;

public sealed class UpdateWorkItemStatusRequest
{
    public WorkItemStatus Status { get; init; }

    public string? UpdatedBy { get; init; }
}
