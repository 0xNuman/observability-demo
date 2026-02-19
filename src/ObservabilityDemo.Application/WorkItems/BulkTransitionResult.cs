namespace ObservabilityDemo.Application.WorkItems;

public sealed record BulkTransitionResult(
    int UpdatedCount,
    int RejectedCount);
