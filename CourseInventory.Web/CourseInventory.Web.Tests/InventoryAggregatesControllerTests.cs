using CourseInventory.Web.Controllers.Api;
using CourseInventory.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CourseInventory.Web.Tests;

public class InventoryAggregatesControllerTests
{
    [Fact]
    public async Task Get_InvalidToken_ReturnsUnauthorized()
    {
        var controller = CreateController(new StubInventoryAggregateExportService(null));

        var result = await controller.Get("invalid-token", CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task Get_UsesQueryStringToken()
    {
        var service = new CapturingInventoryAggregateExportService(new InventoryAggregateExportDto("Books", [], [], []));
        var controller = CreateController(service);

        var result = await controller.Get("query-token", CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal("query-token", service.Token);
    }

    [Fact]
    public async Task Get_UsesBearerTokenWhenQueryTokenMissing()
    {
        var service = new CapturingInventoryAggregateExportService(new InventoryAggregateExportDto("Books", [], [], []));
        var controller = CreateController(service);
        controller.ControllerContext.HttpContext.Request.Headers.Authorization = "Bearer bearer-token";

        var result = await controller.Get(null, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal("bearer-token", service.Token);
    }

    private sealed class StubInventoryAggregateExportService(InventoryAggregateExportDto? result)
        : IInventoryAggregateExportService
    {
        public Task<InventoryAggregateExportDto?> BuildForTokenAsync(
            string token,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
    }

    private static InventoryAggregatesController CreateController(IInventoryAggregateExportService service) =>
        new(service, NullLogger<InventoryAggregatesController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

    private sealed class CapturingInventoryAggregateExportService(InventoryAggregateExportDto? result)
        : IInventoryAggregateExportService
    {
        public string Token { get; private set; } = string.Empty;

        public Task<InventoryAggregateExportDto?> BuildForTokenAsync(
            string token,
            CancellationToken cancellationToken = default)
        {
            Token = token;
            return Task.FromResult(result);
        }
    }
}
