using CourseInventory.Web.Models;
using CourseInventory.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CourseInventory.Web.Controllers;

public class DiscussionController(
    IDiscussionService discussion,
    IAccessService access,
    UserManager<ApplicationUser> users) : Controller
{
    public async Task<IActionResult> Latest(int inventoryId)
    {
        var user = User.Identity?.IsAuthenticated == true ? await users.GetUserAsync(User) : null;
        var accessState = await access.GetAccessAsync(inventoryId, user);
        if (!accessState.CanRead)
        {
            return user is null ? NotFound() : Forbid();
        }

        return Json((await discussion.LatestAsync(inventoryId)).Select(m => new
        {
            author = m.Author?.UserName,
            createdAt = m.CreatedAt.ToString("u"),
            html = m.BodyHtml
        }));
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(int inventoryId, string body)
    {
        var actor = (await users.GetUserAsync(User))!;
        var accessState = await access.GetAccessAsync(inventoryId, actor);
        // Discussion follows the product rule "authenticated user + CanRead(inventory)".
        // Item editing and inventory management still require CanWrite / CanManage elsewhere.
        if (!accessState.CanRead)
        {
            return Forbid();
        }

        var (result, _) = await discussion.AddAsync(inventoryId, body, actor);
        if (!result.Success)
        {
            TempData["Error"] = result.Error ?? "Message could not be posted.";
        }

        return RedirectToAction("Details", "Inventories", new { id = inventoryId });
    }
}
