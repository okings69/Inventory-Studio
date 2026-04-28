using CourseInventory.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace CourseInventory.Web.Controllers;

public class TagsController(ITagService tags) : Controller
{
    public async Task<IActionResult> Autocomplete(string term) => Json(await tags.AutocompleteAsync(term ?? string.Empty));
}
