using CourseInventory.Web.Models;
using CourseInventory.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CourseInventory.Web.Controllers;

[Authorize]
public class AccessController(IAccessService access, UserManager<ApplicationUser> users) : Controller
{
    public async Task<IActionResult> Users(string term) => Json((await access.FindUsersAsync(term)).Select(u => new { u.Id, u.UserName, u.Email }));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Grant(int inventoryId, string userId)
    {
        var result = await access.GrantAsync(inventoryId, userId, (await users.GetUserAsync(User))!);
        TempData[result.Success ? "Success" : "Error"] = result.Error ?? "Access granted.";
        return RedirectToAction("Details", "Inventories", new { id = inventoryId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Revoke(int inventoryId, string userId)
    {
        var result = await access.RevokeAsync(inventoryId, userId, (await users.GetUserAsync(User))!);
        TempData[result.Success ? "Success" : "Error"] = result.Error ?? "Access revoked.";
        return RedirectToAction("Details", "Inventories", new { id = inventoryId });
    }
}
