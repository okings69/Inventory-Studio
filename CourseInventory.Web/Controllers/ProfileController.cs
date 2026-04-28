using CourseInventory.Web.Data;
using CourseInventory.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CourseInventory.Web.Controllers;

[Authorize]
public class ProfileController(ApplicationDbContext db, UserManager<ApplicationUser> users) : Controller
{
    public async Task<IActionResult> Index()
    {
        var user = (await users.GetUserAsync(User))!;
        ViewBag.Owned = await db.Inventories.AsNoTracking().Where(i => i.OwnerId == user.Id).OrderByDescending(i => i.UpdatedAt).ToListAsync();
        ViewBag.Writable = await db.InventoryAccesses.AsNoTracking().Include(a => a.Inventory)
            .Where(a => a.UserId == user.Id)
            .Select(a => a.Inventory!)
            .OrderByDescending(i => i.UpdatedAt)
            .ToListAsync();
        return View();
    }
}
