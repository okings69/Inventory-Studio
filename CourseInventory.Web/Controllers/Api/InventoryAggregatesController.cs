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
        var resolvedToken = ResolveToken(token);
        var result = await aggregates.BuildForTokenAsync(resolvedToken, cancellationToken);
        if (result is null)
        {
            logger.LogWarning("Inventory aggregate API request rejected because the token is missing or invalid.");
            return Unauthorized();
        }

        return Ok(result);
    }

    private string ResolveToken(string? queryToken)
    {
        if (!string.IsNullOrWhiteSpace(queryToken))
        {
            return queryToken;
        }

        var authorization = Request.Headers.Authorization.ToString();
        const string bearerPrefix = "Bearer ";
        return authorization.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase)
            ? authorization[bearerPrefix.Length..].Trim()
            : string.Empty;
    }
}
