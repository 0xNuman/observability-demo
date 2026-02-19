using Dapper;
using Npgsql;
using ObservabilityDemo.Application.Abstractions.Persistence;
using ObservabilityDemo.Application.WorkItems;
using ObservabilityDemo.Domain.Enums;
using ObservabilityDemo.Infrastructure.Persistence;

namespace ObservabilityDemo.Infrastructure.Persistence.WorkItems;

public sealed class DapperWorkItemRepository(PostgresConnectionString connectionString) : IWorkItemRepository
{
    private const string SelectProjection = """
        id,
        tenant_id AS TenantId,
        title,
        description,
        status,
        priority,
        created_at_utc AS CreatedAtUtc,
        updated_at_utc AS UpdatedAtUtc,
        created_by AS CreatedBy,
        updated_by AS UpdatedBy
        """;

    public async Task<WorkItemDto> CreateAsync(
        Guid tenantId,
        Guid id,
        string title,
        string? description,
        WorkItemPriority priority,
        string createdBy,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken = default)
    {
        const string sql = $"""
            INSERT INTO work_items (
                id,
                tenant_id,
                title,
                description,
                status,
                priority,
                created_at_utc,
                updated_at_utc,
                created_by,
                updated_by
            )
            VALUES (
                @Id,
                @TenantId,
                @Title,
                @Description,
                @Status,
                @Priority,
                @CreatedAtUtc,
                @UpdatedAtUtc,
                @CreatedBy,
                @UpdatedBy
            )
            RETURNING {SelectProjection};
            """;

        await using var connection = await CreateOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            commandText: sql,
            parameters: new
            {
                Id = id,
                TenantId = tenantId,
                Title = title,
                Description = description,
                Status = WorkItemStatus.New.ToString(),
                Priority = priority.ToString(),
                CreatedAtUtc = createdAtUtc,
                UpdatedAtUtc = createdAtUtc,
                CreatedBy = createdBy,
                UpdatedBy = createdBy,
            },
            cancellationToken: cancellationToken);

        var row = await connection.QuerySingleAsync<WorkItemRow>(command);
        return row.ToDto();
    }

    public async Task<WorkItemDto?> GetByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        const string sql = $"""
            SELECT {SelectProjection}
            FROM work_items
            WHERE tenant_id = @TenantId AND id = @Id;
            """;

        await using var connection = await CreateOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            commandText: sql,
            parameters: new
            {
                TenantId = tenantId,
                Id = id,
            },
            cancellationToken: cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<WorkItemRow>(command);
        return row?.ToDto();
    }

    public async Task<IReadOnlyList<WorkItemDto>> ListAsync(
        Guid tenantId,
        WorkItemStatus? status,
        int offset,
        int limit,
        CancellationToken cancellationToken = default)
    {
        const string sql = $"""
            SELECT {SelectProjection}
            FROM work_items
            WHERE tenant_id = @TenantId
              AND (@Status IS NULL OR status = @Status)
            ORDER BY created_at_utc DESC
            OFFSET @Offset
            LIMIT @Limit;
            """;

        await using var connection = await CreateOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            commandText: sql,
            parameters: new
            {
                TenantId = tenantId,
                Status = status?.ToString(),
                Offset = offset,
                Limit = limit,
            },
            cancellationToken: cancellationToken);

        var rows = await connection.QueryAsync<WorkItemRow>(command);
        return rows.Select(static row => row.ToDto()).ToArray();
    }

    public async Task<int> CountAsync(
        Guid tenantId,
        WorkItemStatus? status,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT COUNT(*)
            FROM work_items
            WHERE tenant_id = @TenantId
              AND (@Status IS NULL OR status = @Status);
            """;

        await using var connection = await CreateOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            commandText: sql,
            parameters: new
            {
                TenantId = tenantId,
                Status = status?.ToString(),
            },
            cancellationToken: cancellationToken);

        return await connection.QuerySingleAsync<int>(command);
    }

    public async Task<WorkItemDto?> UpdateStatusAsync(
        Guid tenantId,
        Guid id,
        WorkItemStatus targetStatus,
        string updatedBy,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken = default)
    {
        const string sql = $"""
            UPDATE work_items
            SET
                status = @TargetStatus,
                updated_at_utc = @UpdatedAtUtc,
                updated_by = @UpdatedBy
            WHERE tenant_id = @TenantId
              AND id = @Id
              AND status NOT IN ('Done', 'Cancelled')
              AND status <> @TargetStatus
            RETURNING {SelectProjection};
            """;

        await using var connection = await CreateOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            commandText: sql,
            parameters: new
            {
                TenantId = tenantId,
                Id = id,
                TargetStatus = targetStatus.ToString(),
                UpdatedAtUtc = updatedAtUtc,
                UpdatedBy = updatedBy,
            },
            cancellationToken: cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<WorkItemRow>(command);
        return row?.ToDto();
    }

    public async Task<BulkTransitionResult> BulkTransitionAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> workItemIds,
        WorkItemStatus targetStatus,
        string changedBy,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT updated_count AS UpdatedCount, rejected_count AS RejectedCount
            FROM sp_work_items_bulk_transition(
                @TenantId,
                @WorkItemIds,
                @TargetStatus,
                @ChangedBy,
                @CorrelationId
            );
            """;

        await using var connection = await CreateOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            commandText: sql,
            parameters: new
            {
                TenantId = tenantId,
                WorkItemIds = workItemIds.ToArray(),
                TargetStatus = targetStatus.ToString(),
                ChangedBy = changedBy,
                CorrelationId = correlationId,
            },
            cancellationToken: cancellationToken);

        return await connection.QuerySingleAsync<BulkTransitionResult>(command);
    }

    private async Task<NpgsqlConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(connectionString.Value);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private sealed class WorkItemRow
    {
        public Guid Id { get; init; }

        public Guid TenantId { get; init; }

        public string Title { get; init; } = string.Empty;

        public string? Description { get; init; }

        public string Status { get; init; } = string.Empty;

        public string Priority { get; init; } = string.Empty;

        public DateTimeOffset CreatedAtUtc { get; init; }

        public DateTimeOffset UpdatedAtUtc { get; init; }

        public string CreatedBy { get; init; } = string.Empty;

        public string UpdatedBy { get; init; } = string.Empty;

        public WorkItemDto ToDto()
        {
            return new WorkItemDto(
                Id,
                TenantId,
                Title,
                Description,
                ParseStatus(Status),
                ParsePriority(Priority),
                CreatedAtUtc,
                UpdatedAtUtc,
                CreatedBy,
                UpdatedBy);
        }

        private static WorkItemStatus ParseStatus(string value)
        {
            if (!Enum.TryParse<WorkItemStatus>(value, ignoreCase: true, out var status))
            {
                throw new InvalidOperationException($"Unknown work item status value '{value}'.");
            }

            return status;
        }

        private static WorkItemPriority ParsePriority(string value)
        {
            if (!Enum.TryParse<WorkItemPriority>(value, ignoreCase: true, out var priority))
            {
                throw new InvalidOperationException($"Unknown work item priority value '{value}'.");
            }

            return priority;
        }
    }
}
