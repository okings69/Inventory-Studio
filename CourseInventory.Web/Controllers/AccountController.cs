using CourseInventory.Web.Models;
using CourseInventory.Web.Services;
using CourseInventory.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CourseInventory.Web.Controllers;

public class AccountController(
    IConfiguration configuration,
    IWebHostEnvironment environment,
    UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn,
    IUserActivityService userActivity) : Controller
{
    private const string GoogleProvider = "Google";
    private const string FacebookProvider = "Facebook";

    [HttpGet]
    public async Task<IActionResult> Register()
    {
        await PopulateExternalLoginStateAsync();
        return View(new RegisterViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        await PopulateExternalLoginStateAsync();
        if (!ModelState.IsValid) return View(model);
        var user = new ApplicationUser { UserName = model.UserName, Email = model.Email, EmailConfirmed = true };
        var result = await users.CreateAsync(user, model.Password);
        if (result.Succeeded)
        {
            await users.AddToRoleAsync(user, "User");
            await signIn.SignInAsync(user, false);
            return RedirectToAction("Index", "Home");
        }
        ModelState.AddModelError(string.Empty, "Registration could not be completed. Check your email, username, and password.");
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Login(string? returnUrl = null, string? externalError = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        await PopulateExternalLoginStateAsync();
        if (!string.IsNullOrWhiteSpace(externalError))
        {
            var providerName = string.Equals(externalError, "facebook", StringComparison.OrdinalIgnoreCase)
                ? FacebookProvider
                : GoogleProvider;
            ModelState.AddModelError(string.Empty, $"{providerName} login is temporarily unavailable. Please use email login or try again later.");
        }
        return View(new LoginViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        await PopulateExternalLoginStateAsync();
        if (!ModelState.IsValid) return View(model);
        var user = await users.FindByEmailAsync(model.EmailOrUserName) ?? await users.FindByNameAsync(model.EmailOrUserName);
        if (user is null || user.IsBlocked)
        {
            ModelState.AddModelError(string.Empty, "Invalid login.");
            return View(model);
        }
        var result = await signIn.PasswordSignInAsync(user, model.Password, model.RememberMe, false);
        if (result.Succeeded)
        {
            await userActivity.RecordLoginAsync(user);
            return LocalRedirect(returnUrl ?? "/");
        }
        ModelState.AddModelError(string.Empty, "Invalid login.");
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ExternalLogin(string provider, string? returnUrl = null)
    {
        var availableProviders = await signIn.GetExternalAuthenticationSchemesAsync();
        var scheme = availableProviders.FirstOrDefault(p => string.Equals(p.Name, provider, StringComparison.OrdinalIgnoreCase));
        if (scheme is null)
        {
            return await RenderLoginWithErrorAsync(GetProviderUnavailableMessage(provider), returnUrl);
        }

        var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Account", new { returnUrl });
        var properties = signIn.ConfigureExternalAuthenticationProperties(scheme.Name, redirectUrl);
        return Challenge(properties, scheme.Name);
    }

    public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null)
    {
        if (!string.IsNullOrWhiteSpace(remoteError))
        {
            return await RenderLoginWithErrorAsync("External login could not be completed. Please use email login or try again later.", returnUrl);
        }

        var info = await signIn.GetExternalLoginInfoAsync();
        if (info is null)
        {
            return await RenderLoginWithErrorAsync("External login could not be completed. Please use email login or try again later.", returnUrl);
        }

        var result = await signIn.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, false);
        if (result.Succeeded)
        {
            var existingUser = await users.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
            if (existingUser is not null)
            {
                if (existingUser.IsBlocked)
                {
                    await signIn.SignOutAsync();
                    return await RenderLoginWithErrorAsync("External login could not be completed. Please contact support if this continues.", returnUrl);
                }

                await userActivity.RecordLoginAsync(existingUser);
            }

            return LocalRedirect(returnUrl ?? "/");
        }

        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(email))
        {
            return await RenderLoginWithErrorAsync("External login could not be completed because the provider did not return a verified email address.", returnUrl);
        }

        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = await BuildExternalUserNameAsync(email),
                Email = email,
                EmailConfirmed = true
            };

            var create = await users.CreateAsync(user);
            if (!create.Succeeded)
            {
                return await RenderLoginWithErrorAsync("External account could not be created. Please use email registration or try again later.", returnUrl);
            }

            await users.AddToRoleAsync(user, "User");
        }

        if (user.IsBlocked)
        {
            return await RenderLoginWithErrorAsync("External login could not be completed. Please contact support if this continues.", returnUrl);
        }

        var addLogin = await users.AddLoginAsync(user, info);
        if (!addLogin.Succeeded && addLogin.Errors.Any(error => error.Code != "LoginAlreadyAssociated"))
        {
            return await RenderLoginWithErrorAsync("External login could not be linked. Please use email login or try again later.", returnUrl);
        }

        await signIn.SignInAsync(user, false);
        await userActivity.RecordLoginAsync(user);
        return LocalRedirect(returnUrl ?? "/");
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    public IActionResult AccessDenied() => View();

    private async Task<IActionResult> RenderLoginWithErrorAsync(string message, string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        await PopulateExternalLoginStateAsync();
        ModelState.AddModelError(string.Empty, message);
        return View(nameof(Login), new LoginViewModel());
    }

    private async Task PopulateExternalLoginStateAsync()
    {
        ViewBag.ExternalLogins = await signIn.GetExternalAuthenticationSchemesAsync();
        ViewBag.ExternalProviderMessages = BuildExternalProviderMessages();
    }

    private IReadOnlyList<string> BuildExternalProviderMessages()
    {
        var messages = new List<string>();

        if (!environment.IsDevelopment())
        {
            return messages;
        }

        if (!HasProviderCredentials("Authentication:Google:ClientId", "Authentication:Google:ClientSecret"))
        {
            messages.Add("Google login is hidden until Authentication:Google:ClientId and Authentication:Google:ClientSecret are provided through user-secrets or environment variables.");
        }

        if (!HasProviderCredentials("Authentication:Facebook:AppId", "Authentication:Facebook:AppSecret"))
        {
            messages.Add("Facebook login is hidden until Authentication:Facebook:AppId and Authentication:Facebook:AppSecret are provided through user-secrets or environment variables.");
        }

        return messages;
    }

    private string GetProviderUnavailableMessage(string provider)
    {
        if (string.Equals(provider, FacebookProvider, StringComparison.OrdinalIgnoreCase))
        {
            return HasProviderCredentials("Authentication:Facebook:AppId", "Authentication:Facebook:AppSecret")
                ? "Facebook login is currently unavailable. Restart the application and try again."
                : "Facebook login is not configured on this server.";
        }

        if (string.Equals(provider, GoogleProvider, StringComparison.OrdinalIgnoreCase))
        {
            return HasProviderCredentials("Authentication:Google:ClientId", "Authentication:Google:ClientSecret")
                ? "Google login is currently unavailable. Restart the application and try again."
                : "Google login is not configured on this server.";
        }

        return "This external login provider is not available right now.";
    }

    private bool HasProviderCredentials(string key1, string key2) =>
        !string.IsNullOrWhiteSpace(configuration[key1]) &&
        !string.IsNullOrWhiteSpace(configuration[key2]);

    private async Task<string> BuildExternalUserNameAsync(string email)
    {
        var localPart = email.Split('@', 2)[0].Trim();
        var baseUserName = string.IsNullOrWhiteSpace(localPart) ? "user" : localPart;
        var candidate = baseUserName;
        var suffix = 1;

        while (await users.FindByNameAsync(candidate) is not null)
        {
            candidate = $"{baseUserName}{suffix}";
            suffix++;
        }

        return candidate;
    }
}
