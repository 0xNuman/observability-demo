namespace ObservabilityDemo.Application.WorkItems;

public interface IWorkItemService
{
    Task<WorkItemDto> CreateAsync(
        Guid tenantId,
        CreateWorkItemCommand command,
        CancellationToken cancellationToken = default);

    Task<WorkItemDto?> GetByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken cancellationToken = default);

    Task<WorkItemListResult> ListAsync(
        Guid tenantId,
        ListWorkItemsQuery query,
        CancellationToken cancellationToken = default);

    Task<WorkItemDto?> UpdateStatusAsync(
        Guid tenantId,
        Guid id,
        UpdateWorkItemStatusCommand command,
        CancellationToken cancellationToken = default);

    Task<BulkTransitionResult> BulkTransitionAsync(
        Guid tenantId,
        BulkTransitionCommand command,
        CancellationToken cancellationToken = default);
}
