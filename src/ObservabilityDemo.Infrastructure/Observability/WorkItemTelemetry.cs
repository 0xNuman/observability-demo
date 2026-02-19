using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ObservabilityDemo.Infrastructure.Observability;

public static class WorkItemTelemetry
{
    public const string MeterName = "ObservabilityDemo.Infrastructure.WorkItems";

    // Custom business metrics complement framework auto-instrumentation so bulk
    // transition throughput/rejections can be monitored directly in SLI panels.
    private static readonly Meter Meter = new(MeterName);
    private static readonly Histogram<int> BulkTransitionBatchSize = Meter.CreateHistogram<int>(
        "work_items.bulk_transition.batch_size",
        unit: "items",
        description: "Number of work items requested in each bulk transition operation.");
    private static readonly Counter<int> BulkTransitionUpdatedCount = Meter.CreateCounter<int>(
        "work_items.bulk_transition.updated_count",
        unit: "items",
        description: "Number of work items successfully transitioned in bulk operations.");
    private static readonly Counter<int> BulkTransitionRejectedCount = Meter.CreateCounter<int>(
        "work_items.bulk_transition.rejected_count",
        unit: "items",
        description: "Number of work items rejected in bulk operations.");

    public static void RecordBulkTransition(int batchSize, int updatedCount, int rejectedCount, string targetStatus)
    {
        var tags = new TagList
        {
            { "target_status", targetStatus },
        };

        BulkTransitionBatchSize.Record(batchSize, tags);
        BulkTransitionUpdatedCount.Add(updatedCount, tags);
        BulkTransitionRejectedCount.Add(rejectedCount, tags);
    }
}
