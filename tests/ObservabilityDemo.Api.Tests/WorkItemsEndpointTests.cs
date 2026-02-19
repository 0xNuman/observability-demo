using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ObservabilityDemo.Application.WorkItems;
using ObservabilityDemo.Domain.Enums;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ObservabilityDemo.Api.Tests;

public sealed class WorkItemsEndpointTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task List_WhenTenantHeaderMissing_ReturnsBadRequest()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/work-items");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Invalid tenant header", problem.Title);
    }

    [Fact]
    public async Task List_WhenTenantHeaderInvalid_ReturnsBadRequest()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", "not-a-guid");

        var response = await client.GetAsync("/work-items");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task List_WhenTenantHeaderValid_ReturnsOk()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TenantId.ToString());

        var response = await client.GetAsync("/work-items?page=1&pageSize=20");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<WorkItemListResult>(JsonOptions);
        Assert.NotNull(payload);
        Assert.Single(payload.Items);
    }

    [Fact]
    public async Task Create_WithValidPayload_ReturnsCreated()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TenantId.ToString());

        var response = await client.PostAsJsonAsync(
            "/work-items",
            new
            {
                title = "Review telemetry spike",
                description = "Check trace and logs correlation.",
                priority = "High",
                requestedBy = "api-test",
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<WorkItemDto>(JsonOptions);
        Assert.NotNull(payload);
        Assert.Equal("Review telemetry spike", payload.Title);
        Assert.Equal(WorkItemPriority.High, payload.Priority);
    }

    [Fact]
    public async Task UpdateStatus_WhenTransitionRejected_ReturnsConflict()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TenantId.ToString());

        var response = await client.PatchAsJsonAsync(
            $"/work-items/{Guid.NewGuid():D}/status",
            new
            {
                status = "Done",
                updatedBy = "api-test",
            });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task HealthPrometheus_ReturnsPrometheusFormattedMetrics()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/prometheus");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "text/plain",
            response.Content.Headers.ContentType?.MediaType);

        var payload = await response.Content.ReadAsStringAsync();
        Assert.Contains("api_up 1", payload, StringComparison.Ordinal);
        Assert.Contains("api_process_uptime_seconds", payload, StringComparison.Ordinal);
    }
}

public sealed class ApiTestFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IWorkItemService>();
            services.AddSingleton<IWorkItemService>(new StubWorkItemService());
        });
    }
}

internal sealed class StubWorkItemService : IWorkItemService
{
    public Task<WorkItemDto> CreateAsync(
        Guid tenantId,
        CreateWorkItemCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Title))
        {
            throw new ArgumentException("Work item title is required.", nameof(command.Title));
        }

        return Task.FromResult(
            BuildItem(
                id: Guid.NewGuid(),
                tenantId: tenantId,
                title: command.Title,
                description: command.Description,
                status: WorkItemStatus.New,
                priority: command.Priority));
    }

    public Task<WorkItemDto?> GetByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<WorkItemDto?>(
            BuildItem(
                id: id,
                tenantId: tenantId,
                title: "Stub item",
                description: null,
                status: WorkItemStatus.InProgress,
                priority: WorkItemPriority.Medium));
    }

    public Task<WorkItemListResult> ListAsync(
        Guid tenantId,
        ListWorkItemsQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.Page <= 0)
        {
            throw new ArgumentException("Page must be greater than zero.", nameof(query.Page));
        }

        var items = new[]
        {
            BuildItem(
                id: Guid.Parse("22222222-2222-2222-2222-222222222222"),
                tenantId: tenantId,
                title: "Stub list item",
                description: "List response from test service.",
                status: query.Status ?? WorkItemStatus.New,
                priority: WorkItemPriority.High),
        };

        return Task.FromResult(new WorkItemListResult(items, query.Page, query.PageSize, 1));
    }

    public Task<WorkItemDto?> UpdateStatusAsync(
        Guid tenantId,
        Guid id,
        UpdateWorkItemStatusCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.TargetStatus == WorkItemStatus.Done)
        {
            throw new InvalidOperationException("Completed work items cannot transition to a new state.");
        }

        return Task.FromResult<WorkItemDto?>(
            BuildItem(
                id: id,
                tenantId: tenantId,
                title: "Stub item",
                description: null,
                status: command.TargetStatus,
                priority: WorkItemPriority.Medium));
    }

    public Task<BulkTransitionResult> BulkTransitionAsync(
        Guid tenantId,
        BulkTransitionCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.WorkItemIds.Count == 0)
        {
            throw new ArgumentException("At least one work item id is required.", nameof(command.WorkItemIds));
        }

        return Task.FromResult(new BulkTransitionResult(command.WorkItemIds.Count, 0));
    }

    private static WorkItemDto BuildItem(
        Guid id,
        Guid tenantId,
        string title,
        string? description,
        WorkItemStatus status,
        WorkItemPriority priority)
    {
        var now = DateTimeOffset.UtcNow;
        return new WorkItemDto(
            id,
            tenantId,
            title,
            description,
            status,
            priority,
            now,
            now,
            "stub",
            "stub");
    }
}
