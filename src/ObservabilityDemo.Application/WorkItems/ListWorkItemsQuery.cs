using ObservabilityDemo.Domain.Enums;

namespace ObservabilityDemo.Application.WorkItems;

public sealed record ListWorkItemsQuery(
    WorkItemStatus? Status,
    int Page,
    int PageSize);
