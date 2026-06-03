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
    ISupportTicketService supportTickets,
    UserManager<ApplicationUser> users,
    ILogger<InventoriesController> logger) : Controller
{
    public async Task<IActionResult> Index(string? q, string? tag, string? accessFilter)
    {
        var user = User.Identity?.IsAuthenticated == true ? await users.GetUserAsync(User) : null;
        var scope = await access.BuildScopeAsync(user);

        var query = access.FilterReadableInventories(
                db.Inventories.AsNoTracking(),
                scope)
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

        var user = User.Identity?.IsAuthenticated == true ? await users.GetUserAsync(User) : null;
        var accessState = await access.GetAccessAsync(id, user);
        if (!accessState.CanRead)
        {
            return user is null ? NotFound() : Forbid();
        }

        inventory.Items = await db.InventoryItems.AsNoTracking()
            .Where(i => i.InventoryId == id)
            .Include(i => i.Likes)
            .OrderByDescending(i => i.UpdatedAt)
            .ToListAsync();

        logger.LogInformation(
            "Inventories.Details loaded InventoryId={InventoryId} with {ItemsCount} items.",
            id,
            inventory.Items.Count);

        return View(new InventoryDetailsViewModel
        {
            Inventory = inventory,
            Access = accessState,
            Stats = await stats.BuildAsync(id),
            CustomIdPreview = await customIds.PreviewAsync(id)
        });
    }

    [Authorize]
    public IActionResult Create() => View(new InventoryFormViewModel());

    [Authorize, HttpGet("Inventories/{id:int}/Help")]
    public async Task<IActionResult> Help(int id)
    {
        var user = (await users.GetUserAsync(User))!;
        var inventory = await db.Inventories.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id);
        if (inventory is null)
        {
            return NotFound();
        }

        var accessState = await access.GetAccessAsync(id, user);
        if (!accessState.CanRead)
        {
            return Forbid();
        }

        return View(new SupportTicketViewModel
        {
            InventoryId = inventory.Id,
            InventoryTitle = inventory.Title,
            Priority = "Average"
        });
    }

    [Authorize, HttpPost("Inventories/{id:int}/Help"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Help(int id, SupportTicketViewModel model, CancellationToken cancellationToken)
    {
        var user = (await users.GetUserAsync(User))!;
        var inventory = await db.Inventories.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id);
        if (inventory is null)
        {
            return NotFound();
        }

        var accessState = await access.GetAccessAsync(id, user);
        if (!accessState.CanRead)
        {
            return Forbid();
        }

        model.InventoryId = inventory.Id;
        model.InventoryTitle = inventory.Title;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var link = Url.Action(nameof(Details), "Inventories", new { id = inventory.Id }, Request.Scheme)
            ?? $"{Request.Scheme}://{Request.Host}/Inventories/Details/{inventory.Id}";

        try
        {
            await supportTickets.SubmitAsync(new SupportTicketRequest(
                ReportedBy: user.UserName ?? user.Email ?? user.Id,
                ReportedByEmail: user.Email,
                Inventory: inventory.Title,
                Link: link,
                Priority: model.Priority,
                Summary: model.Summary), cancellationToken);
        }
        catch (SupportTicketException ex)
        {
            logger.LogWarning(ex, "Support ticket submission failed for InventoryId={InventoryId}", inventory.Id);
            ModelState.AddModelError("", ex.Message);
            AddGoogleDriveConsentLinkIfNeeded(ex, inventory.Id);
            return View(model);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Unexpected support ticket submission failure for InventoryId={InventoryId}", inventory.Id);
            ModelState.AddModelError("", "Support ticket could not be sent right now. Please try again later.");
            return View(model);
        }

        TempData["Success"] = "Support ticket created. Admins will be notified shortly.";
        return RedirectToAction(nameof(Details), new { id = inventory.Id });
    }

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
        var actor = (await users.GetUserAsync(User))!;
        var accessState = await access.GetAccessAsync(model.Id, actor);
        if (!accessState.CanManage)
        {
            return Forbid();
        }

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
        }, model.Tags, actor);

        if (Request.Headers.XRequestedWith == "XMLHttpRequest")
            return Json(new { ok = result.Success, error = result.Error });
        TempData[result.Success ? "Success" : "Error"] = result.Error ?? "Saved.";
        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    [Authorize, HttpGet]
    public IActionResult Delete(int? id)
    {
        if (id.GetValueOrDefault() > 0)
        {
            return RedirectToAction(nameof(Details), new { id });
        }

        return RedirectToAction(nameof(Index));
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var actor = (await users.GetUserAsync(User))!;
        var accessState = await access.GetAccessAsync(id, actor);
        if (!accessState.CanManage)
        {
            return Forbid();
        }

        var result = await inventories.DeleteAsync(id, actor);
        TempData[result.Success ? "Success" : "Error"] = result.Error ?? "Inventory deleted.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteMany([FromForm] int[] ids)
    {
        var distinctIds = ids.Distinct().ToArray();
        if (distinctIds.Length == 0)
        {
            TempData["Error"] = "No inventories selected.";
            return RedirectToAction(nameof(Index));
        }

        var user = (await users.GetUserAsync(User))!;
        var scope = await access.BuildScopeAsync(user);
        var manageableIds = await db.Inventories.AsNoTracking()
            .Where(i => distinctIds.Contains(i.Id) && (scope.IsAdmin || i.OwnerId == user.Id))
            .Select(i => i.Id)
            .ToArrayAsync();

        if (manageableIds.Length > 0)
        {
            var rows = await db.Inventories
                .Where(i => manageableIds.Contains(i.Id))
                .ToListAsync();
            db.Inventories.RemoveRange(rows);
            await db.SaveChangesAsync();
        }

        TempData[manageableIds.Length > 0 ? "Success" : "Error"] = manageableIds.Length > 0
            ? $"{manageableIds.Length} inventory deleted."
            : "No inventory could be deleted.";
        return RedirectToAction(nameof(Index));
    }

    private void AddGoogleDriveConsentLinkIfNeeded(SupportTicketException exception, int inventoryId)
    {
        if (!string.Equals(exception.Message, GoogleDriveTicketUploadService.MissingOAuthTokenMessage, StringComparison.Ordinal) &&
            !string.Equals(exception.Message, GoogleDriveTicketUploadService.ConsentRequiredMessage, StringComparison.Ordinal))
        {
            return;
        }

        var returnUrl = Url.Action(nameof(Help), "Inventories", new { id = inventoryId }) ?? $"/Inventories/{inventoryId}/Help";
        ViewBag.GoogleDriveConsentUrl = Url.Action("GoogleDriveConsent", "Account", new { returnUrl });
    }
}
