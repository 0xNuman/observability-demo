using Microsoft.AspNetCore.Mvc;
using ObservabilityDemo.Api.Controllers;

namespace ObservabilityDemo.Api.Tests;

public sealed class DiagnosticsControllerTests
{
    [Fact]
    public async Task SlowAsync_WithOutOfRangeDelay_ReturnsBadRequest()
    {
        var controller = new DiagnosticsController();

        var result = await controller.SlowAsync(-1, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }
}
