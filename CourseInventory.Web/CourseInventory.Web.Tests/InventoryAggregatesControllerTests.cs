using CourseInventory.Web.Controllers.Api;
using CourseInventory.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CourseInventory.Web.Tests;

public class InventoryAggregatesControllerTests
{
    [Fact]
    public async Task Get_InvalidToken_ReturnsUnauthorized()
    {
        var controller = new InventoryAggregatesController(
            new StubInventoryAggregateExportService(null),
            NullLogger<InventoryAggregatesController>.Instance);

        var result = await controller.Get("invalid-token", CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    private sealed class StubInventoryAggregateExportService(InventoryAggregateExportDto? result)
        : IInventoryAggregateExportService
    {
        public Task<InventoryAggregateExportDto?> BuildForTokenAsync(
            string token,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
    }
}
