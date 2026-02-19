using Microsoft.AspNetCore.Mvc;

namespace ObservabilityDemo.Api.Controllers;

[ApiController]
[Route("diagnostics")]
public sealed class DiagnosticsController : ControllerBase
{
    [HttpGet("slow")]
    public async Task<IActionResult> SlowAsync([FromQuery] int delayMs = 1500, CancellationToken cancellationToken = default)
    {
        if (delayMs < 0 || delayMs > 30000)
        {
            return BadRequest("delayMs must be between 0 and 30000.");
        }

        await Task.Delay(delayMs, cancellationToken);

        return Ok(
            new
            {
                scenario = "slow",
                delayMs,
                utcNow = DateTimeOffset.UtcNow,
            });
    }

    [HttpGet("fail")]
    public IActionResult Fail()
    {
        throw new InvalidOperationException("Intentional failure endpoint for observability validation.");
    }
}
