using ObservabilityDemo.Domain.Enums;

namespace ObservabilityDemo.Domain.Entities;

public sealed class WorkItem
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public string Title { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public WorkItemStatus Status { get; private set; } = WorkItemStatus.New;

    public WorkItemPriority Priority { get; private set; } = WorkItemPriority.Medium;

    public DateTimeOffset CreatedAtUtc { get; init; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static WorkItem Create(
        Guid id,
        Guid tenantId,
        string title,
        string? description,
        WorkItemPriority priority,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Work item id is required.", nameof(id));
        }

        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant id is required.", nameof(tenantId));
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Work item title is required.", nameof(title));
        }

        return new WorkItem
        {
            Id = id,
            TenantId = tenantId,
            Title = title.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            Priority = priority,
            Status = WorkItemStatus.New,
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = createdAtUtc,
        };
    }

    public void UpdateStatus(WorkItemStatus targetStatus, DateTimeOffset updatedAtUtc)
    {
        if (Status == WorkItemStatus.Cancelled || Status == WorkItemStatus.Done)
        {
            throw new InvalidOperationException("Completed work items cannot transition to a new state.");
        }

        Status = targetStatus;
        UpdatedAtUtc = updatedAtUtc;
    }
}
