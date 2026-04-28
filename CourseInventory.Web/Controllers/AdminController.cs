using CourseInventory.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CourseInventory.Web.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController(UserManager<ApplicationUser> users) : Controller
{
    public async Task<IActionResult> Users() => View(await users.Users.OrderBy(u => u.UserName).ToListAsync());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleBlock(string id)
    {
        var user = await users.FindByIdAsync(id);
        if (user is not null)
        {
            user.IsBlocked = !user.IsBlocked;
            await users.UpdateAsync(user);
        }
        return RedirectToAction(nameof(Users));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleAdmin(string id)
    {
        var user = await users.FindByIdAsync(id);
        if (user is not null)
        {
            if (await users.IsInRoleAsync(user, "Admin")) await users.RemoveFromRoleAsync(user, "Admin");
            else await users.AddToRoleAsync(user, "Admin");
        }
        return RedirectToAction(nameof(Users));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var user = await users.FindByIdAsync(id);
        if (user is not null) await users.DeleteAsync(user);
        return RedirectToAction(nameof(Users));
    }
}
