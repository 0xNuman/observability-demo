using ObservabilityDemo.Domain.Entities;
using ObservabilityDemo.Domain.Enums;

namespace ObservabilityDemo.IntegrationTests;

public sealed class WorkItemDomainFlowTests
{
    [Fact]
    public void UpdateStatus_WhenItemIsDone_ThrowsInvalidOperationException()
    {
        var item = WorkItem.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Check alerts",
            "Validate p95 latency spikes.",
            WorkItemPriority.High,
            DateTimeOffset.UtcNow);

        item.UpdateStatus(WorkItemStatus.Done, DateTimeOffset.UtcNow);

        var act = () => item.UpdateStatus(WorkItemStatus.InProgress, DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(act);
    }
}
