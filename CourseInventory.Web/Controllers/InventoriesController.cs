using CourseInventory.Web.Data;
using CourseInventory.Web.Models;
using CourseInventory.Web.Models.Inventory;
using CourseInventory.Web.Services;
using CourseInventory.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CourseInventory.Web.Controllers;

public class InventoriesController(
    ApplicationDbContext db,
    IInventoryService inventories,
    IAccessService access,
    IStatsService stats,
    ICustomIdService customIds,
    UserManager<ApplicationUser> users,
    ILogger<InventoriesController> logger) : Controller
{
    public async Task<IActionResult> Index(string? q, string? tag, string? accessFilter)
    {
        var query = db.Inventories.AsNoTracking()
            .Include(i => i.Owner)
            .Include(i => i.InventoryTags).ThenInclude(t => t.Tag)
            .AsQueryable();
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(i =>
            EF.Functions.ILike(i.Title, $"%{q}%") ||
            EF.Functions.ILike(i.Category, $"%{q}%") ||
            (i.Owner != null && EF.Functions.ILike(i.Owner.UserName!, $"%{q}%")));
        if (!string.IsNullOrWhiteSpace(tag)) query = query.Where(i => i.InventoryTags.Any(t => t.Tag!.NormalizedName == tag.ToUpper()));
        if (string.Equals(accessFilter, "public", StringComparison.OrdinalIgnoreCase)) query = query.Where(i => i.IsPublic);
        if (string.Equals(accessFilter, "private", StringComparison.OrdinalIgnoreCase)) query = query.Where(i => !i.IsPublic);

        var inventoriesList = await query
            .OrderByDescending(i => i.UpdatedAt)
            .ToListAsync();

        var inventoryIds = inventoriesList.Select(i => i.Id).ToArray();
        var itemCounts = inventoryIds.Length == 0
            ? new Dictionary<int, int>()
            : await db.InventoryItems.AsNoTracking()
                .Where(item => inventoryIds.Contains(item.InventoryId))
                .GroupBy(item => item.InventoryId)
                .Select(group => new { InventoryId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(row => row.InventoryId, row => row.Count);

        foreach (var inventory in inventoriesList)
        {
            inventory.ItemCount = itemCounts.GetValueOrDefault(inventory.Id);
        }

        return View(inventoriesList);
    }

    public async Task<IActionResult> Details(int id)
    {
        var inventory = await inventories.GetDetailsAsync(id);
        if (inventory is null) return NotFound();

        inventory.Items = await db.InventoryItems.AsNoTracking()
            .Where(i => i.InventoryId == id)
            .Include(i => i.Likes)
            .OrderByDescending(i => i.UpdatedAt)
            .ToListAsync();

        logger.LogInformation(
            "Inventories.Details loaded InventoryId={InventoryId} with {ItemsCount} items.",
            id,
            inventory.Items.Count);

        var user = User.Identity?.IsAuthenticated == true ? await users.GetUserAsync(User) : null;
        return View(new InventoryDetailsViewModel
        {
            Inventory = inventory,
            Access = await access.GetAccessAsync(id, user),
            Stats = await stats.BuildAsync(id),
            CustomIdPreview = await customIds.PreviewAsync(id)
        });
    }

    [Authorize]
    public IActionResult Create() => View(new InventoryFormViewModel());

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(InventoryFormViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = (await users.GetUserAsync(User))!;
        var inventory = await inventories.CreateAsync(new Inventory
        {
            Title = model.Title,
            DescriptionMarkdown = model.DescriptionMarkdown,
            Category = model.Category,
            ImageUrl = model.ImageUrl,
            StatusOptions = model.StatusOptions,
            IsPublic = model.IsPublic
        }, user, model.Tags);
        return RedirectToAction(nameof(Details), new { id = inventory.Id });
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSettings(InventoryFormViewModel model)
    {
        var result = await inventories.UpdateSettingsAsync(new Inventory
        {
            Id = model.Id,
            Title = model.Title,
            DescriptionMarkdown = model.DescriptionMarkdown,
            Category = model.Category,
            ImageUrl = model.ImageUrl,
            StatusOptions = model.StatusOptions,
            IsPublic = model.IsPublic,
            RowVersion = model.RowVersion
        }, model.Tags, (await users.GetUserAsync(User))!);

        if (Request.Headers.XRequestedWith == "XMLHttpRequest")
            return Json(new { ok = result.Success, error = result.Error });
        TempData[result.Success ? "Success" : "Error"] = result.Error ?? "Saved.";
        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await inventories.DeleteAsync(id, (await users.GetUserAsync(User))!);
        TempData[result.Success ? "Success" : "Error"] = result.Error ?? "Inventory deleted.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteMany([FromForm] int[] ids)
    {
        if (ids.Length == 0)
        {
            TempData["Error"] = "No inventories selected.";
            return RedirectToAction(nameof(Index));
        }

        var user = (await users.GetUserAsync(User))!;
        var deleted = 0;
        foreach (var id in ids.Distinct())
        {
            var result = await inventories.DeleteAsync(id, user);
            if (result.Success) deleted++;
        }

        TempData[deleted > 0 ? "Success" : "Error"] = deleted > 0
            ? $"{deleted} inventory deleted."
            : "No inventory could be deleted.";
        return RedirectToAction(nameof(Index));
    }
}
