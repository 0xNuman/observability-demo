using ObservabilityDemo.Domain.Enums;

namespace ObservabilityDemo.Application.WorkItems;

public sealed record WorkItemDto(
    Guid Id,
    Guid TenantId,
    string Title,
    string? Description,
    WorkItemStatus Status,
    WorkItemPriority Priority,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string CreatedBy,
    string UpdatedBy);
