using ObservabilityDemo.Application.Abstractions.Persistence;
using ObservabilityDemo.Application.WorkItems;
using ObservabilityDemo.Domain.Enums;

namespace ObservabilityDemo.IntegrationTests;

public sealed class WorkItemServiceTests
{
    [Fact]
    public async Task ListAsync_WhenPageIsInvalid_ThrowsArgumentException()
    {
        var service = new WorkItemService(new FakeWorkItemRepository());

        var act = async () => await service.ListAsync(
            tenantId: Guid.NewGuid(),
            query: new ListWorkItemsQuery(Status: null, Page: 0, PageSize: 20),
            cancellationToken: CancellationToken.None);

        await Assert.ThrowsAsync<ArgumentException>(act);
    }

    [Fact]
    public async Task BulkTransitionAsync_DeduplicatesIds_AndUsesDefaultActor()
    {
        var repository = new FakeWorkItemRepository();
        var service = new WorkItemService(repository);
        var tenantId = Guid.NewGuid();
        var repeatedId = Guid.NewGuid();

        await service.BulkTransitionAsync(
            tenantId,
            new BulkTransitionCommand(
                WorkItemIds: new[] { repeatedId, repeatedId },
                TargetStatus: WorkItemStatus.Blocked,
                ChangedBy: null,
                CorrelationId: "corr-123"),
            CancellationToken.None);

        Assert.NotNull(repository.LastBulkTransitionCall);
        Assert.Single(repository.LastBulkTransitionCall.WorkItemIds);
        Assert.Equal("api", repository.LastBulkTransitionCall.ChangedBy);
        Assert.Equal("corr-123", repository.LastBulkTransitionCall.CorrelationId);
    }

    [Fact]
    public async Task UpdateStatusAsync_WhenItemAlreadyDone_ThrowsInvalidOperationException()
    {
        var repository = new FakeWorkItemRepository
        {
            StoredItem = BuildItem(status: WorkItemStatus.Done),
        };

        var service = new WorkItemService(repository);

        var act = async () => await service.UpdateStatusAsync(
            tenantId: repository.StoredItem.TenantId,
            id: repository.StoredItem.Id,
            command: new UpdateWorkItemStatusCommand(WorkItemStatus.InProgress, UpdatedBy: "tester"),
            cancellationToken: CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(act);
    }

    private static WorkItemDto BuildItem(WorkItemStatus status)
    {
        var now = DateTimeOffset.UtcNow;
        return new WorkItemDto(
            Id: Guid.NewGuid(),
            TenantId: Guid.NewGuid(),
            Title: "Test item",
            Description: "Used in tests.",
            Status: status,
            Priority: WorkItemPriority.Medium,
            CreatedAtUtc: now,
            UpdatedAtUtc: now,
            CreatedBy: "tester",
            UpdatedBy: "tester");
    }

    private sealed class FakeWorkItemRepository : IWorkItemRepository
    {
        public WorkItemDto StoredItem { get; set; } = BuildItem(WorkItemStatus.New);

        public BulkTransitionCall? LastBulkTransitionCall { get; private set; }

        public Task<WorkItemDto> CreateAsync(
            Guid tenantId,
            Guid id,
            string title,
            string? description,
            WorkItemPriority priority,
            string createdBy,
            DateTimeOffset createdAtUtc,
            CancellationToken cancellationToken = default)
        {
            var item = new WorkItemDto(
                id,
                tenantId,
                title,
                description,
                WorkItemStatus.New,
                priority,
                createdAtUtc,
                createdAtUtc,
                createdBy,
                createdBy);
            StoredItem = item;
            return Task.FromResult(item);
        }

        public Task<WorkItemDto?> GetByIdAsync(
            Guid tenantId,
            Guid id,
            CancellationToken cancellationToken = default)
        {
            if (StoredItem.Id != id || StoredItem.TenantId != tenantId)
            {
                return Task.FromResult<WorkItemDto?>(null);
            }

            return Task.FromResult<WorkItemDto?>(StoredItem);
        }

        public Task<IReadOnlyList<WorkItemDto>> ListAsync(
            Guid tenantId,
            WorkItemStatus? status,
            int offset,
            int limit,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<WorkItemDto> items = [StoredItem];
            return Task.FromResult(items);
        }

        public Task<int> CountAsync(
            Guid tenantId,
            WorkItemStatus? status,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(1);
        }

        public Task<WorkItemDto?> UpdateStatusAsync(
            Guid tenantId,
            Guid id,
            WorkItemStatus targetStatus,
            string updatedBy,
            DateTimeOffset updatedAtUtc,
            CancellationToken cancellationToken = default)
        {
            if (StoredItem.Id != id || StoredItem.TenantId != tenantId)
            {
                return Task.FromResult<WorkItemDto?>(null);
            }

            StoredItem = StoredItem with
            {
                Status = targetStatus,
                UpdatedAtUtc = updatedAtUtc,
                UpdatedBy = updatedBy,
            };
            return Task.FromResult<WorkItemDto?>(StoredItem);
        }

        public Task<BulkTransitionResult> BulkTransitionAsync(
            Guid tenantId,
            IReadOnlyCollection<Guid> workItemIds,
            WorkItemStatus targetStatus,
            string changedBy,
            string correlationId,
            CancellationToken cancellationToken = default)
        {
            LastBulkTransitionCall = new BulkTransitionCall(
                TenantId: tenantId,
                WorkItemIds: workItemIds.ToArray(),
                TargetStatus: targetStatus,
                ChangedBy: changedBy,
                CorrelationId: correlationId);

            return Task.FromResult(new BulkTransitionResult(workItemIds.Count, 0));
        }
    }

    private sealed record BulkTransitionCall(
        Guid TenantId,
        IReadOnlyCollection<Guid> WorkItemIds,
        WorkItemStatus TargetStatus,
        string ChangedBy,
        string CorrelationId);
}
