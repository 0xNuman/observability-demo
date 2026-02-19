using Microsoft.AspNetCore.Mvc;

namespace ObservabilityDemo.Api.Controllers;

[ApiController]
[Route("work-items")]
public sealed class WorkItemsController : ControllerBase
{
    [HttpPost]
    public IActionResult Create()
    {
        return StatusCode(
            StatusCodes.Status501NotImplemented,
            new
            {
                message = "Work item create endpoint will be implemented in the next milestone.",
            });
    }

    [HttpGet("{id:guid}")]
    public IActionResult GetById(Guid id)
    {
        return StatusCode(
            StatusCodes.Status501NotImplemented,
            new
            {
                message = "Work item read endpoint will be implemented in the next milestone.",
                id,
            });
    }

    [HttpGet]
    public IActionResult List()
    {
        return StatusCode(
            StatusCodes.Status501NotImplemented,
            new
            {
                message = "Work item list endpoint will be implemented in the next milestone.",
            });
    }

    [HttpPatch("{id:guid}/status")]
    public IActionResult UpdateStatus(Guid id)
    {
        return StatusCode(
            StatusCodes.Status501NotImplemented,
            new
            {
                message = "Work item status endpoint will be implemented in the next milestone.",
                id,
            });
    }

    [HttpPost("bulk-transition")]
    public IActionResult BulkTransition()
    {
        return StatusCode(
            StatusCodes.Status501NotImplemented,
            new
            {
                message = "Bulk transition endpoint will be implemented with a PostgreSQL stored procedure in the next milestone.",
            });
    }
}
