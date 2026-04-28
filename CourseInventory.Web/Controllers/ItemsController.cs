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

public class ItemsController(
    ApplicationDbContext db,
    IItemService items,
    UserManager<ApplicationUser> users,
    ILogger<ItemsController> logger) : Controller
{
    [Authorize]
    public async Task<IActionResult> Create(int inventoryId) => View("Edit", new ItemFormViewModel
    {
        Item = new InventoryItem { InventoryId = inventoryId },
        Inventory = (await db.Inventories.AsNoTracking().FirstAsync(i => i.Id == inventoryId)),
        Fields = await db.InventoryFields.AsNoTracking().Where(f => f.InventoryId == inventoryId).OrderBy(f => f.SortOrder).ToListAsync()
    });

    [Authorize]
    public async Task<IActionResult> Details(int id)
    {
        var item = await items.GetAsync(id);
        if (item is null) return NotFound();
        return View("Edit", new ItemFormViewModel
        {
            Item = item,
            Inventory = item.Inventory ?? new Inventory { Id = item.InventoryId },
            Fields = await db.InventoryFields.AsNoTracking().Where(f => f.InventoryId == item.InventoryId).OrderBy(f => f.SortOrder).ToListAsync()
        });
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(ItemFormViewModel model)
    {
        var inventoryId = model.Item.InventoryId;

        logger.LogInformation(
            "Items.Save received ItemId={ItemId}, InventoryId={InventoryId}, CustomId={CustomId}",
            model.Item.Id,
            inventoryId,
            model.Item.CustomId);

        model.Item.CustomId = string.IsNullOrWhiteSpace(model.Item.CustomId)
            ? null
            : model.Item.CustomId.Trim();

        ModelState.Remove("Item.CreatedById");
        ModelState.Remove("Item.Inventory");
        ModelState.Remove("Item.CreatedBy");

        if (inventoryId <= 0)
        {
            logger.LogWarning("Items.Save received invalid InventoryId={InventoryId}", inventoryId);
            ModelState.AddModelError("", "InventoryId is missing.");
        }

        if (!ModelState.IsValid)
        {
            model.Inventory = await LoadInventoryAsync(inventoryId);
            model.Fields = await LoadFieldsAsync(inventoryId);
            return View("Edit", model);
        }

        var isNew = model.Item.Id == 0;
        var result = model.Item.Id == 0
            ? await items.CreateAsync(model.Item, (await users.GetUserAsync(User))!)
            : await items.UpdateAsync(model.Item, (await users.GetUserAsync(User))!);

        if (!result.Success)
        {
            ModelState.AddModelError("", result.Error ?? "Item could not be saved.");
            TempData["Error"] = result.Error ?? "Item could not be saved.";
            logger.LogWarning(
                "Items.Save failed for InventoryId={InventoryId}. Error={Error}",
                inventoryId,
                result.Error);
            model.Inventory = await LoadInventoryAsync(inventoryId);
            model.Fields = await LoadFieldsAsync(inventoryId);
            return View("Edit", model);
        }

        var savedItemsCount = await db.InventoryItems.AsNoTracking()
            .CountAsync(i => i.InventoryId == inventoryId);

        logger.LogInformation(
            "Items.Save succeeded for InventoryId={InventoryId}. Inventory now has {ItemsCount} items.",
            inventoryId,
            savedItemsCount);

        TempData["Success"] = isNew ? "Item created successfully" : "Item saved successfully";
        return RedirectToAction("Details", "Inventories", new { id = inventoryId });
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSelected(int inventoryId, int[] ids)
    {
        var result = await items.DeleteManyAsync(inventoryId, ids, (await users.GetUserAsync(User))!);
        TempData[result.Success ? "Success" : "Error"] = result.Error ?? "Items deleted.";
        return RedirectToAction("Details", "Inventories", new { id = inventoryId });
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleLike(int itemId, int inventoryId)
    {
        var user = (await users.GetUserAsync(User))!;
        var result = await items.ToggleLikeAsync(itemId, user);

        if (Request.Headers.XRequestedWith == "XMLHttpRequest")
        {
            if (!result.Success)
            {
                Response.StatusCode = StatusCodes.Status400BadRequest;
                return Json(new { ok = false, error = result.Error ?? "Like could not be updated." });
            }

            var likesCount = await db.ItemLikes.CountAsync(l => l.ItemId == itemId);
            var liked = await db.ItemLikes.AnyAsync(l => l.ItemId == itemId && l.UserId == user.Id);

            return Json(new
            {
                ok = true,
                count = likesCount,
                liked
            });
        }

        return RedirectToAction("Details", "Inventories", new { id = inventoryId });
    }

    private async Task<IReadOnlyList<InventoryField>> LoadFieldsAsync(int inventoryId) =>
        await db.InventoryFields.AsNoTracking()
            .Where(f => f.InventoryId == inventoryId)
            .OrderBy(f => f.SortOrder)
            .ToListAsync();

    private async Task<Inventory> LoadInventoryAsync(int inventoryId) =>
        await db.Inventories.AsNoTracking().FirstAsync(i => i.Id == inventoryId);
}
