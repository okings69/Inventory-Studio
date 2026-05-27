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
    ISalesforceService salesforce) : Controller
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
    public async Task<IActionResult> Salesforce(string? userId = null)
    {
        var targetUser = await FindPermittedTargetUserAsync(userId);
        if (targetUser is null)
        {
            return Forbid();
        }

        return View(BuildSalesforceForm(targetUser));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Salesforce(SalesforceProfileFormViewModel model, CancellationToken cancellationToken)
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

        var result = await salesforce.SendProfileAsync(targetUser, model, cancellationToken);
        if (!result.Success)
        {
            var error = result.Error ?? "Profile could not be sent to Salesforce.";
            ViewData["SalesforceError"] = error;
            return View(model);
        }

        TempData["Success"] = $"Salesforce Account {result.AccountId} and Contact {result.ContactId} were created.";
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

    private static SalesforceProfileFormViewModel BuildSalesforceForm(ApplicationUser user) => new()
    {
        UserId = user.Id,
        UserName = user.UserName ?? user.Email ?? "Inventory Studio user",
        UserEmail = user.Email,
        Phone = user.PhoneNumber
    };
}
