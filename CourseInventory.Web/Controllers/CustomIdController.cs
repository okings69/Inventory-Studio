using CourseInventory.Web.Data;
using CourseInventory.Web.Models;
using CourseInventory.Web.Models.Inventory;
using CourseInventory.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CourseInventory.Web.Controllers;

[Authorize]
public class CustomIdController(ApplicationDbContext db, IAccessService access, ICustomIdService customIds, UserManager<ApplicationUser> users) : Controller
{
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(int inventoryId, CustomIdElementType elementType, string? fixedValue, string? format)
    {
        if (!(await access.GetAccessAsync(inventoryId, (await users.GetUserAsync(User))!)).CanManage) return Forbid();
        var max = await db.CustomIdElements.Where(e => e.InventoryId == inventoryId).Select(e => (int?)e.SortOrder).MaxAsync() ?? 0;
        db.CustomIdElements.Add(new CustomIdElement { InventoryId = inventoryId, ElementType = elementType, FixedValue = fixedValue, Format = format, SortOrder = max + 10 });
        await db.SaveChangesAsync();
        return RedirectToAction("Details", "Inventories", new { id = inventoryId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, int inventoryId)
    {
        if (!(await access.GetAccessAsync(inventoryId, (await users.GetUserAsync(User))!)).CanManage) return Forbid();
        var element = await db.CustomIdElements.FindAsync(id);
        if (element is not null) db.CustomIdElements.Remove(element);
        await db.SaveChangesAsync();
        return RedirectToAction("Details", "Inventories", new { id = inventoryId });
    }

    public async Task<IActionResult> Preview(int inventoryId) => Json(new { preview = await customIds.PreviewAsync(inventoryId) });
}
