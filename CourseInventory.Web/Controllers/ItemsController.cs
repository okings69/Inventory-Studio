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
    IAccessService access,
    UserManager<ApplicationUser> users,
    ILogger<ItemsController> logger) : Controller
{
    [Authorize]
    public async Task<IActionResult> Create(int inventoryId)
    {
        var actor = (await users.GetUserAsync(User))!;
        var accessState = await access.GetAccessAsync(inventoryId, actor);
        if (!accessState.CanWrite)
        {
            return Forbid();
        }

        return View("Edit", new ItemFormViewModel
        {
            Item = new InventoryItem { InventoryId = inventoryId },
            Inventory = (await db.Inventories.AsNoTracking().FirstAsync(i => i.Id == inventoryId)),
            Fields = await db.InventoryFields.AsNoTracking().Where(f => f.InventoryId == inventoryId).OrderBy(f => f.SortOrder).ToListAsync()
        });
    }

    public async Task<IActionResult> Details(int id)
    {
        var item = await items.GetAsync(id);
        if (item is null) return NotFound();

        var actor = await users.GetUserAsync(User);
        var accessState = await access.GetAccessAsync(item.InventoryId, actor);
        if (!accessState.CanRead)
        {
            return Forbid();
        }

        var fields = await LoadFieldsAsync(item.InventoryId);
        return View(new ItemDetailsViewModel
        {
            Item = item,
            Inventory = item.Inventory ?? new Inventory { Id = item.InventoryId },
            Fields = fields,
            AccessState = accessState,
            IsLikedByCurrentUser = actor is not null && item.Likes.Any(l => l.UserId == actor.Id),
            DisplayValues = fields.Select(field => BuildDisplayValue(field, item)).ToList()
        });
    }

    [Authorize]
    public async Task<IActionResult> Edit(int id)
    {
        var item = await items.GetAsync(id);
        if (item is null) return NotFound();

        var actor = (await users.GetUserAsync(User))!;
        var accessState = await access.GetAccessAsync(item.InventoryId, actor);
        if (!accessState.CanWrite)
        {
            return Forbid();
        }

        return View(new ItemFormViewModel
        {
            Item = item,
            Inventory = item.Inventory ?? new Inventory { Id = item.InventoryId },
            Fields = await LoadFieldsAsync(item.InventoryId)
        });
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(ItemFormViewModel model)
    {
        var inventoryId = model.Item.InventoryId;
        var actor = (await users.GetUserAsync(User))!;
        var accessState = await access.GetAccessAsync(inventoryId, actor);
        if (!accessState.CanWrite)
        {
            return Forbid();
        }

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
            ? await items.CreateAsync(model.Item, actor)
            : await items.UpdateAsync(model.Item, actor);

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
    public async Task<IActionResult> Delete(int id)
    {
        var item = await items.GetAsync(id);
        if (item is null) return NotFound();

        var actor = (await users.GetUserAsync(User))!;
        var accessState = await access.GetAccessAsync(item.InventoryId, actor);
        if (!accessState.CanManage)
        {
            return Forbid();
        }

        var inventoryId = item.InventoryId;
        var result = await items.DeleteManyAsync(inventoryId, [id], actor);
        TempData[result.Success ? "Success" : "Error"] = result.Error ?? "Item deleted.";
        return RedirectToAction("Details", "Inventories", new { id = inventoryId });
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSelected(int inventoryId, int[] ids)
    {
        var actor = (await users.GetUserAsync(User))!;
        var accessState = await access.GetAccessAsync(inventoryId, actor);
        if (!accessState.CanManage)
        {
            return Forbid();
        }

        var result = await items.DeleteManyAsync(inventoryId, ids, actor);
        TempData[result.Success ? "Success" : "Error"] = result.Error ?? "Items deleted.";
        return RedirectToAction("Details", "Inventories", new { id = inventoryId });
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleLike(int itemId, int inventoryId, string? returnUrl = null)
    {
        var user = (await users.GetUserAsync(User))!;
        var accessState = await access.GetAccessAsync(inventoryId, user);
        if (!accessState.CanRead)
        {
            return Forbid();
        }

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

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
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

    private static ItemDisplayValueViewModel BuildDisplayValue(InventoryField field, InventoryItem item)
    {
        var rawValue = GetFieldValue(item, field.FieldKey);
        var hasValue = rawValue switch
        {
            null => false,
            string text => !string.IsNullOrWhiteSpace(text),
            _ => true
        };

        return new ItemDisplayValueViewModel
        {
            Field = field,
            RawValue = rawValue,
            DisplayValue = FormatFieldValue(rawValue),
            HasValue = hasValue,
            IsLink = field.FieldType == InventoryFieldType.Link,
            IsBoolean = field.FieldType == InventoryFieldType.Boolean,
            BooleanValue = rawValue as bool?
        };
    }

    private static object? GetFieldValue(InventoryItem item, string key) => key switch
    {
        "Text1" => item.Text1,
        "Text2" => item.Text2,
        "Text3" => item.Text3,
        "LongText1" => item.LongText1,
        "LongText2" => item.LongText2,
        "LongText3" => item.LongText3,
        "Number1" => item.Number1,
        "Number2" => item.Number2,
        "Number3" => item.Number3,
        "Link1" => item.Link1,
        "Link2" => item.Link2,
        "Link3" => item.Link3,
        "Bool1" => item.Bool1,
        "Bool2" => item.Bool2,
        "Bool3" => item.Bool3,
        _ => null
    };

    private static string FormatFieldValue(object? value) => value switch
    {
        null => string.Empty,
        string text => text.Trim(),
        decimal number => number.ToString("0.##"),
        bool boolean => boolean ? "Yes" : "No",
        _ => value.ToString() ?? string.Empty
    };
}
