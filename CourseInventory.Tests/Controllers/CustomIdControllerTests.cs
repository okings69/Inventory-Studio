using CourseInventory.Web.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace CourseInventory.Tests.Controllers;

public class CustomIdControllerTests
{
    [Fact]
    public void AddGet_WithoutInventoryId_RedirectsToInventoriesIndex()
    {
        var controller = new CustomIdController(null!, null!, null!, null!);

        var result = Assert.IsType<RedirectToActionResult>(controller.Add(null));

        Assert.Equal("Index", result.ActionName);
        Assert.Equal("Inventories", result.ControllerName);
    }

    [Fact]
    public void AddGet_WithInventoryId_RedirectsToInventoryDetails()
    {
        var controller = new CustomIdController(null!, null!, null!, null!);

        var result = Assert.IsType<RedirectToActionResult>(controller.Add(42));

        Assert.Equal("Details", result.ActionName);
        Assert.Equal("Inventories", result.ControllerName);
        Assert.Equal(42, result.RouteValues?["id"]);
        Assert.Equal("customid", result.Fragment);
    }
}
