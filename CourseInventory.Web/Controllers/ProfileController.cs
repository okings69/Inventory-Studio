using CourseInventory.Web.Data;
using CourseInventory.Web.Models;
using CourseInventory.Web.Services;
using CourseInventory.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CourseInventory.Web.Controllers;

[Authorize]
public class ProfileController(
    ApplicationDbContext db,
    IAccessService access,
    UserManager<ApplicationUser> users,
    IHubSpotService hubSpot) : Controller
{
    public async Task<IActionResult> Index()
    {
        var user = (await users.GetUserAsync(User))!;
        var scope = await access.BuildScopeAsync(user);
        var accessible = await access.FilterReadableInventories(db.Inventories.AsNoTracking(), scope)
            .OrderByDescending(i => i.UpdatedAt)
            .ToListAsync();
        var explicitWritableIds = scope.IsAdmin
            ? []
            : await db.InventoryAccesses.AsNoTracking()
                .Where(a => a.UserId == user.Id)
                .Select(a => a.InventoryId)
                .ToListAsync();
        var owned = accessible.Where(i => i.OwnerId == user.Id).ToList();
        var writable = accessible
            .Where(i => i.OwnerId != user.Id && (scope.IsAdmin || explicitWritableIds.Contains(i.Id)))
            .ToList();

        return View(new ProfileViewModel
        {
            Owned = owned,
            Writable = writable,
            Accessible = accessible
        });
    }

    [HttpGet]
    public async Task<IActionResult> HubSpot(string? userId = null)
    {
        var targetUser = await FindPermittedTargetUserAsync(userId);
        if (targetUser is null)
        {
            return Forbid();
        }

        return View(BuildHubSpotForm(targetUser));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> HubSpot(HubSpotProfileFormViewModel model, CancellationToken cancellationToken)
    {
        var targetUser = await FindPermittedTargetUserAsync(model.UserId);
        if (targetUser is null)
        {
            return Forbid();
        }

        model.UserName = targetUser.UserName ?? targetUser.Email ?? "Inventory Studio user";
        model.UserEmail = targetUser.Email;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await hubSpot.SendProfileAsync(targetUser, model, cancellationToken);
        if (!result.Success)
        {
            ViewData["HubSpotError"] = result.Error ?? "Profile could not be sent to HubSpot.";
            return View(model);
        }

        var associationText = result.AssociationCreated ? " The contact was associated with the company." : string.Empty;
        TempData["HubSpotSuccess"] = $"HubSpot Company {result.CompanyId} and Contact {result.ContactId} were created.{associationText}";
        return RedirectToAction(nameof(Index));
    }

    private async Task<ApplicationUser?> FindPermittedTargetUserAsync(string? userId)
    {
        var currentUser = await users.GetUserAsync(User);
        if (currentUser is null)
        {
            return null;
        }

        var targetUserId = string.IsNullOrWhiteSpace(userId) ? currentUser.Id : userId;
        if (targetUserId != currentUser.Id && !User.IsInRole("Admin"))
        {
            return null;
        }

        return await users.FindByIdAsync(targetUserId);
    }

    private static HubSpotProfileFormViewModel BuildHubSpotForm(ApplicationUser user) => new()
    {
        UserId = user.Id,
        UserName = user.UserName ?? user.Email ?? "Inventory Studio user",
        UserEmail = user.Email,
        Phone = user.PhoneNumber
    };
}
