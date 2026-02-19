using Microsoft.AspNetCore.Mvc;
using ObservabilityDemo.Api.Contracts.WorkItems;
using ObservabilityDemo.Api.Tenancy;
using ObservabilityDemo.Application.WorkItems;
using ObservabilityDemo.Domain.Enums;

namespace ObservabilityDemo.Api.Controllers;

[ApiController]
[Route("work-items")]
public sealed class WorkItemsController(IWorkItemService workItemService, ITenantContext tenantContext) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateWorkItemRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!tenantContext.HasTenant)
        {
            return MissingTenantResponse();
        }

        try
        {
            var item = await workItemService.CreateAsync(
                tenantContext.TenantId,
                new CreateWorkItemCommand(
                    request.Title,
                    request.Description,
                    request.Priority,
                    request.RequestedBy),
                cancellationToken);

            return CreatedAtAction(nameof(GetById), new { id = item.Id }, item);
        }
        catch (ArgumentException exception)
        {
            return ValidationErrorResponse(exception.Message);
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        if (!tenantContext.HasTenant)
        {
            return MissingTenantResponse();
        }

        try
        {
            var item = await workItemService.GetByIdAsync(
                tenantContext.TenantId,
                id,
                cancellationToken);

            return item is null ? NotFound() : Ok(item);
        }
        catch (ArgumentException exception)
        {
            return ValidationErrorResponse(exception.Message);
        }
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] WorkItemStatus? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (!tenantContext.HasTenant)
        {
            return MissingTenantResponse();
        }

        try
        {
            var result = await workItemService.ListAsync(
                tenantContext.TenantId,
                new ListWorkItemsQuery(status, page, pageSize),
                cancellationToken);

            return Ok(result);
        }
        catch (ArgumentException exception)
        {
            return ValidationErrorResponse(exception.Message);
        }
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] UpdateWorkItemStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!tenantContext.HasTenant)
        {
            return MissingTenantResponse();
        }

        try
        {
            var item = await workItemService.UpdateStatusAsync(
                tenantContext.TenantId,
                id,
                new UpdateWorkItemStatusCommand(request.Status, request.UpdatedBy),
                cancellationToken);

            return item is null ? NotFound() : Ok(item);
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(
                new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Status transition rejected",
                    Detail = exception.Message,
                });
        }
        catch (ArgumentException exception)
        {
            return ValidationErrorResponse(exception.Message);
        }
    }

    [HttpPost("bulk-transition")]
    public async Task<IActionResult> BulkTransition(
        [FromBody] BulkTransitionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!tenantContext.HasTenant)
        {
            return MissingTenantResponse();
        }

        try
        {
            var result = await workItemService.BulkTransitionAsync(
                tenantContext.TenantId,
                new BulkTransitionCommand(
                    request.WorkItemIds,
                    request.TargetStatus,
                    request.ChangedBy,
                    request.CorrelationId ?? HttpContext.TraceIdentifier),
                cancellationToken);

            return Ok(result);
        }
        catch (ArgumentException exception)
        {
            return ValidationErrorResponse(exception.Message);
        }
    }

    private BadRequestObjectResult MissingTenantResponse()
    {
        return BadRequest(
            new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid tenant header",
                Detail = "X-Tenant-Id header is required for tenant-scoped endpoints.",
            });
    }

    private BadRequestObjectResult ValidationErrorResponse(string message)
    {
        return BadRequest(
            new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation failed",
                Detail = message,
            });
    }
}
