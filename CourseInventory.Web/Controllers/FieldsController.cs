using CourseInventory.Web.Models;
using CourseInventory.Web.Models.Inventory;
using CourseInventory.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CourseInventory.Web.Controllers;

[Authorize]
public class FieldsController(IFieldService fields, IAccessService access, UserManager<ApplicationUser> users) : Controller
{
    private const string FieldsTab = "fields";

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(int inventoryId, InventoryFieldType fieldType, string title, string? description)
    {
        if (!(await access.GetAccessAsync(inventoryId, (await users.GetUserAsync(User))!)).CanManage) return Forbid();
        var result = await fields.AddAsync(inventoryId, fieldType, title, description);
        TempData[result.Success ? "Success" : "Error"] = result.Error ?? "Field added.";
        return RedirectToAction("Details", "Inventories", new { id = inventoryId }, FieldsTab);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(InventoryField field)
    {
        if (!(await access.GetAccessAsync(field.InventoryId, (await users.GetUserAsync(User))!)).CanManage) return Forbid();
        await fields.UpdateAsync(field);
        return RedirectToAction("Details", "Inventories", new { id = field.InventoryId }, FieldsTab);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, int inventoryId)
    {
        if (!(await access.GetAccessAsync(inventoryId, (await users.GetUserAsync(User))!)).CanManage) return Forbid();
        await fields.DeleteAsync(id);
        return RedirectToAction("Details", "Inventories", new { id = inventoryId }, FieldsTab);
    }
}
