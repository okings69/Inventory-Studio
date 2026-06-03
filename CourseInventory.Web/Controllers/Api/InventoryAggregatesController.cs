using CourseInventory.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace CourseInventory.Web.Controllers.Api;

[ApiController]
[Route("api/inventories/aggregates")]
public class InventoryAggregatesController(
    IInventoryAggregateExportService aggregates,
    ILogger<InventoryAggregatesController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? token, CancellationToken cancellationToken)
    {
        var result = await aggregates.BuildForTokenAsync(token ?? string.Empty, cancellationToken);
        if (result is null)
        {
            logger.LogWarning("Inventory aggregate API request rejected because the token is missing or invalid.");
            return Unauthorized();
        }

        return Ok(result);
    }
}
