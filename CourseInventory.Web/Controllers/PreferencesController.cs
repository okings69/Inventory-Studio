using CourseInventory.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace CourseInventory.Web.Controllers;

public class PreferencesController(UserManager<ApplicationUser> users) : Controller
{
    [AllowAnonymous]
    [AcceptVerbs("GET", "POST")]
    public async Task<IActionResult> Set(string? language, string? theme, string? returnUrl)
    {
        if (language is "en" or "fr")
        {
            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(language)),
                new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true });
        }

        if (User.Identity?.IsAuthenticated == true)
        {
            var user = await users.GetUserAsync(User);
            if (user is not null)
            {
                if (language is "en" or "fr") user.PreferredLanguage = language;
                if (theme is "light" or "dark") user.PreferredTheme = theme;
                await users.UpdateAsync(user);
            }
        }

        return LocalRedirect(returnUrl ?? "/");
    }
}
