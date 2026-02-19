using ObservabilityDemo.Application.WorkItems;
using ObservabilityDemo.Domain.Enums;

namespace ObservabilityDemo.Application.Abstractions.Persistence;

public interface IWorkItemRepository
{
    Task<WorkItemDto> CreateAsync(
        Guid tenantId,
        Guid id,
        string title,
        string? description,
        WorkItemPriority priority,
        string createdBy,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken = default);

    Task<WorkItemDto?> GetByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkItemDto>> ListAsync(
        Guid tenantId,
        WorkItemStatus? status,
        int offset,
        int limit,
        CancellationToken cancellationToken = default);

    Task<int> CountAsync(
        Guid tenantId,
        WorkItemStatus? status,
        CancellationToken cancellationToken = default);

    Task<WorkItemDto?> UpdateStatusAsync(
        Guid tenantId,
        Guid id,
        WorkItemStatus targetStatus,
        string updatedBy,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken = default);

    Task<BulkTransitionResult> BulkTransitionAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> workItemIds,
        WorkItemStatus targetStatus,
        string changedBy,
        string correlationId,
        CancellationToken cancellationToken = default);
}
