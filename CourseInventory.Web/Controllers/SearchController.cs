using CourseInventory.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using CourseInventory.Web.Models;

namespace CourseInventory.Web.Controllers;

public class SearchController(ISearchService search, UserManager<ApplicationUser> users) : Controller
{
    public async Task<IActionResult> Index(string q)
    {
        ViewBag.Query = q;
        var user = User.Identity?.IsAuthenticated == true ? await users.GetUserAsync(User) : null;
        return View(await search.SearchAsync(q ?? string.Empty, user));
    }
}
