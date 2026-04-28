using CourseInventory.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace CourseInventory.Web.Controllers;

[Authorize]
public class PreferencesController(UserManager<ApplicationUser> users) : Controller
{
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Set(string? language, string? theme, string? returnUrl)
    {
        var user = (await users.GetUserAsync(User))!;
        if (language is "en" or "fr")
        {
            user.PreferredLanguage = language;
            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(language)),
                new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true });
        }
        if (theme is "light" or "dark") user.PreferredTheme = theme;
        await users.UpdateAsync(user);
        return LocalRedirect(returnUrl ?? "/");
    }
}
