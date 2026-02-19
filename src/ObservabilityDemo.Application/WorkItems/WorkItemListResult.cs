namespace ObservabilityDemo.Application.WorkItems;

public sealed record WorkItemListResult(
    IReadOnlyList<WorkItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount);
