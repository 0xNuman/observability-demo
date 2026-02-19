using ObservabilityDemo.Application.Abstractions.Persistence;
using ObservabilityDemo.Domain.Entities;
using ObservabilityDemo.Domain.Enums;

namespace ObservabilityDemo.Application.WorkItems;

public sealed class WorkItemService(IWorkItemRepository repository) : IWorkItemService
{
    private const int MaxPageSize = 200;
    private const int MaxActorLength = 100;
    private const string DefaultActor = "api";

    public async Task<WorkItemDto> CreateAsync(
        Guid tenantId,
        CreateWorkItemCommand command,
        CancellationToken cancellationToken = default)
    {
        ValidateTenantId(tenantId);

        if (command is null)
        {
            throw new ArgumentNullException(nameof(command));
        }

        var createdAtUtc = DateTimeOffset.UtcNow;
        var actor = NormalizeActor(command.RequestedBy, nameof(command.RequestedBy));

        var workItem = WorkItem.Create(
            id: Guid.NewGuid(),
            tenantId: tenantId,
            title: command.Title,
            description: command.Description,
            priority: command.Priority,
            createdAtUtc: createdAtUtc);

        return await repository.CreateAsync(
            tenantId: tenantId,
            id: workItem.Id,
            title: workItem.Title,
            description: workItem.Description,
            priority: workItem.Priority,
            createdBy: actor,
            createdAtUtc: createdAtUtc,
            cancellationToken: cancellationToken);
    }

    public Task<WorkItemDto?> GetByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        ValidateTenantId(tenantId);

        if (id == Guid.Empty)
        {
            throw new ArgumentException("Work item id is required.", nameof(id));
        }

        return repository.GetByIdAsync(tenantId, id, cancellationToken);
    }

    public async Task<WorkItemListResult> ListAsync(
        Guid tenantId,
        ListWorkItemsQuery query,
        CancellationToken cancellationToken = default)
    {
        ValidateTenantId(tenantId);

        if (query is null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        if (query.Page <= 0)
        {
            throw new ArgumentException("Page must be greater than zero.", nameof(query.Page));
        }

        if (query.PageSize <= 0 || query.PageSize > MaxPageSize)
        {
            throw new ArgumentException($"PageSize must be between 1 and {MaxPageSize}.", nameof(query.PageSize));
        }

        var offset = (query.Page - 1) * query.PageSize;
        var items = await repository.ListAsync(
            tenantId: tenantId,
            status: query.Status,
            offset: offset,
            limit: query.PageSize,
            cancellationToken: cancellationToken);

        var totalCount = await repository.CountAsync(
            tenantId: tenantId,
            status: query.Status,
            cancellationToken: cancellationToken);

        return new WorkItemListResult(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<WorkItemDto?> UpdateStatusAsync(
        Guid tenantId,
        Guid id,
        UpdateWorkItemStatusCommand command,
        CancellationToken cancellationToken = default)
    {
        ValidateTenantId(tenantId);

        if (id == Guid.Empty)
        {
            throw new ArgumentException("Work item id is required.", nameof(id));
        }

        if (command is null)
        {
            throw new ArgumentNullException(nameof(command));
        }

        var existing = await repository.GetByIdAsync(tenantId, id, cancellationToken);
        if (existing is null)
        {
            return null;
        }

        if (existing.Status is WorkItemStatus.Done or WorkItemStatus.Cancelled)
        {
            throw new InvalidOperationException("Completed work items cannot transition to a new state.");
        }

        if (existing.Status == command.TargetStatus)
        {
            return existing;
        }

        var updated = await repository.UpdateStatusAsync(
            tenantId: tenantId,
            id: id,
            targetStatus: command.TargetStatus,
            updatedBy: NormalizeActor(command.UpdatedBy, nameof(command.UpdatedBy)),
            updatedAtUtc: DateTimeOffset.UtcNow,
            cancellationToken: cancellationToken);

        return updated ?? await repository.GetByIdAsync(tenantId, id, cancellationToken);
    }

    public Task<BulkTransitionResult> BulkTransitionAsync(
        Guid tenantId,
        BulkTransitionCommand command,
        CancellationToken cancellationToken = default)
    {
        ValidateTenantId(tenantId);

        if (command is null)
        {
            throw new ArgumentNullException(nameof(command));
        }

        if (command.WorkItemIds is null || command.WorkItemIds.Count == 0)
        {
            throw new ArgumentException("At least one work item id is required.", nameof(command.WorkItemIds));
        }

        if (command.WorkItemIds.Any(static id => id == Guid.Empty))
        {
            throw new ArgumentException("Work item ids cannot include empty GUID values.", nameof(command.WorkItemIds));
        }

        return repository.BulkTransitionAsync(
            tenantId: tenantId,
            workItemIds: command.WorkItemIds.Distinct().ToArray(),
            targetStatus: command.TargetStatus,
            changedBy: NormalizeActor(command.ChangedBy, nameof(command.ChangedBy)),
            correlationId: NormalizeCorrelationId(command.CorrelationId),
            cancellationToken: cancellationToken);
    }

    private static void ValidateTenantId(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant id is required.", nameof(tenantId));
        }
    }

    private static string NormalizeActor(string? actor, string paramName)
    {
        var normalized = string.IsNullOrWhiteSpace(actor) ? DefaultActor : actor.Trim();
        if (normalized.Length > MaxActorLength)
        {
            throw new ArgumentException(
                $"Actor value cannot exceed {MaxActorLength} characters.",
                paramName);
        }

        return normalized;
    }

    private static string NormalizeCorrelationId(string? correlationId)
    {
        var normalized = string.IsNullOrWhiteSpace(correlationId)
            ? Guid.NewGuid().ToString("N")
            : correlationId.Trim();

        return normalized.Length <= 100 ? normalized : normalized[..100];
    }
}
