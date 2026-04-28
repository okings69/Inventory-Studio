using CourseInventory.Web.Data;
using CourseInventory.Web.Models;
using CourseInventory.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CourseInventory.Web.Controllers;

public class HomeController(IInventoryService inventories, ApplicationDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        ViewBag.Latest = await inventories.LatestAsync();
        ViewBag.Popular = await inventories.PopularAsync();
        ViewBag.Tags = await db.Tags.AsNoTracking()
            .OrderByDescending(t => t.InventoryTags.Count)
            .Take(30)
            .ToListAsync();
        ViewBag.TotalInventories = await db.Inventories.AsNoTracking().CountAsync();
        ViewBag.TotalItems = await db.InventoryItems.AsNoTracking().CountAsync();
        ViewBag.TotalUsers = await db.Users.AsNoTracking().CountAsync();
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View(new ErrorViewModel { RequestId = HttpContext.TraceIdentifier });
}
