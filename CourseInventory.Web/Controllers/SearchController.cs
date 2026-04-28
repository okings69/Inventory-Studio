using CourseInventory.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace CourseInventory.Web.Controllers;

public class SearchController(ISearchService search) : Controller
{
    public async Task<IActionResult> Index(string q)
    {
        ViewBag.Query = q;
        return View(await search.SearchAsync(q ?? string.Empty));
    }
}
