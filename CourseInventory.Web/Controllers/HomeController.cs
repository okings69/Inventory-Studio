using CourseInventory.Web.Data;
using CourseInventory.Web.Models;
using CourseInventory.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CourseInventory.Web.Controllers;

public class HomeController(
    ApplicationDbContext db,
    IAccessService access,
    UserManager<ApplicationUser> users) : Controller
{
    public async Task<IActionResult> Index()
    {
        var user = User.Identity?.IsAuthenticated == true ? await users.GetUserAsync(User) : null;
        var scope = await access.BuildScopeAsync(user);
        var readableInventories = access.FilterReadableInventories(db.Inventories.AsNoTracking(), scope);
        var readableInventoryIds = readableInventories.Select(i => i.Id);

        var latest = await readableInventories
            .Include(i => i.Owner)
            .Include(i => i.InventoryTags).ThenInclude(t => t.Tag)
            .OrderByDescending(i => i.CreatedAt)
            .Take(12)
            .ToListAsync();

        var popular = await readableInventories
            .Include(i => i.Owner)
            .OrderByDescending(i => i.Items.Count)
            .ThenByDescending(i => i.UpdatedAt)
            .Take(5)
            .ToListAsync();

        await ApplyItemCountsAsync(latest);
        await ApplyItemCountsAsync(popular);

        ViewBag.Latest = latest;
        ViewBag.Popular = popular;
        ViewBag.Tags = await db.Tags.AsNoTracking()
            .Where(t => t.InventoryTags.Any(it => readableInventoryIds.Contains(it.InventoryId)))
            .OrderByDescending(t => t.InventoryTags.Count(it => readableInventoryIds.Contains(it.InventoryId)))
            .Take(30)
            .ToListAsync();
        ViewBag.TotalInventories = await readableInventories.CountAsync();
        ViewBag.TotalItems = await db.InventoryItems.AsNoTracking()
            .Where(i => readableInventoryIds.Contains(i.InventoryId))
            .CountAsync();
        ViewBag.TotalUsers = await db.Users.AsNoTracking().CountAsync();
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View(new ErrorViewModel { RequestId = HttpContext.TraceIdentifier });

    private async Task ApplyItemCountsAsync(IReadOnlyList<Models.Inventory.Inventory> inventories)
    {
        if (inventories.Count == 0)
        {
            return;
        }

        var inventoryIds = inventories.Select(i => i.Id).ToArray();
        var counts = await db.InventoryItems.AsNoTracking()
            .Where(item => inventoryIds.Contains(item.InventoryId))
            .GroupBy(item => item.InventoryId)
            .Select(group => new { InventoryId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.InventoryId, row => row.Count);

        foreach (var inventory in inventories)
        {
            inventory.ItemCount = counts.GetValueOrDefault(inventory.Id);
        }
    }
}
