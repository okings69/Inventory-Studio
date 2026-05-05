using CourseInventory.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace CourseInventory.Web.Controllers;

public class TagsController(ITagService tags) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Autocomplete(string term) => Json(await tags.AutocompleteAsync(term ?? string.Empty));

    [HttpGet]
    public async Task<IActionResult> Suggest(string term) => Json(await tags.AutocompleteAsync(term ?? string.Empty));
}
