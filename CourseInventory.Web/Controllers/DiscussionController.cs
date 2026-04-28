using CourseInventory.Web.Models;
using CourseInventory.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CourseInventory.Web.Controllers;

public class DiscussionController(IDiscussionService discussion, UserManager<ApplicationUser> users) : Controller
{
    public async Task<IActionResult> Latest(int inventoryId) => Json((await discussion.LatestAsync(inventoryId)).Select(m => new
    {
        author = m.Author?.UserName,
        createdAt = m.CreatedAt.ToString("u"),
        html = m.BodyHtml
    }));

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(int inventoryId, string body)
    {
        await discussion.AddAsync(inventoryId, body, (await users.GetUserAsync(User))!);
        return RedirectToAction("Details", "Inventories", new { id = inventoryId });
    }
}
