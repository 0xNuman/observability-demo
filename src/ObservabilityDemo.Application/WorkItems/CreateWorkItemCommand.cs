using ObservabilityDemo.Domain.Enums;

namespace ObservabilityDemo.Application.WorkItems;

public sealed record CreateWorkItemCommand(
    string Title,
    string? Description,
    WorkItemPriority Priority,
    string? RequestedBy);
