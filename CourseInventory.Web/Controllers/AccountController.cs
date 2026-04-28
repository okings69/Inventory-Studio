using CourseInventory.Web.Models;
using CourseInventory.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CourseInventory.Web.Controllers;

public class AccountController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn) : Controller
{
    [HttpGet]
    public IActionResult Register() => View(new RegisterViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = new ApplicationUser { UserName = model.UserName, Email = model.Email, EmailConfirmed = true };
        var result = await users.CreateAsync(user, model.Password);
        if (result.Succeeded)
        {
            await users.AddToRoleAsync(user, "User");
            await signIn.SignInAsync(user, false);
            return RedirectToAction("Index", "Home");
        }
        foreach (var error in result.Errors) ModelState.AddModelError("", error.Description);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Login(string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        ViewBag.ExternalLogins = await signIn.GetExternalAuthenticationSchemesAsync();
        return View(new LoginViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        if (!ModelState.IsValid) return View(model);
        var user = await users.FindByEmailAsync(model.EmailOrUserName) ?? await users.FindByNameAsync(model.EmailOrUserName);
        if (user is null || user.IsBlocked)
        {
            ModelState.AddModelError("", "Invalid login or blocked account.");
            return View(model);
        }
        var result = await signIn.PasswordSignInAsync(user, model.Password, model.RememberMe, false);
        if (result.Succeeded) return LocalRedirect(returnUrl ?? "/");
        ModelState.AddModelError("", "Invalid login.");
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult ExternalLogin(string provider, string? returnUrl = null)
    {
        var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Account", new { returnUrl });
        var properties = signIn.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
        return Challenge(properties, provider);
    }

    public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null)
    {
        var info = await signIn.GetExternalLoginInfoAsync();
        if (info is null) return RedirectToAction(nameof(Login));
        var result = await signIn.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, false);
        if (result.Succeeded) return LocalRedirect(returnUrl ?? "/");
        var email = info.Principal.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
        if (string.IsNullOrWhiteSpace(email)) return RedirectToAction(nameof(Login));
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
        var create = await users.CreateAsync(user);
        if (create.Succeeded)
        {
            await users.AddToRoleAsync(user, "User");
            await users.AddLoginAsync(user, info);
            await signIn.SignInAsync(user, false);
            return LocalRedirect(returnUrl ?? "/");
        }
        return RedirectToAction(nameof(Login));
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    public IActionResult AccessDenied() => View();
}
