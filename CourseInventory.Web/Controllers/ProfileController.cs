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
    UserManager<ApplicationUser> users) : Controller
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
}
