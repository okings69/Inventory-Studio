using System.Net;

namespace CourseInventory.Tests.Integration;

public class SecurityIntegrationTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task CreateInventory_PageRedirectsAnonymousUserToLogin()
    {
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync("/Inventories/Create");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task AdminUsers_PageRedirectsAnonymousUserToLogin()
    {
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync("/Admin/Users");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task CreateInventoryPost_RedirectsAnonymousUserBeforeCreatingData()
    {
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Title"] = "Blocked inventory",
            ["Category"] = "Security"
        });

        var response = await client.PostAsync("/Inventories/Create", form);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task HomeResponse_IncludesReasonableSecurityHeaders()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/favicon.ico");

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("strict-origin-when-cross-origin", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Equal("camera=(), microphone=(), geolocation=()", response.Headers.GetValues("Permissions-Policy").Single());
    }

    [Fact]
    public async Task AnonymousUser_CanSetLanguagePreferenceCookie()
    {
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        var loginPage = await client.GetStringAsync("/Account/Login");
        var token = ExtractAntiForgeryToken(loginPage);
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["language"] = "fr",
            ["returnUrl"] = "/Account/Login"
        });

        var response = await client.PostAsync("/Preferences/Set", form);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains(".AspNetCore.Culture", response.Headers.GetValues("Set-Cookie").Single());
    }

    private static string ExtractAntiForgeryToken(string html)
    {
        const string marker = "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"";
        var start = html.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, "Anti-forgery token was not rendered.");
        start += marker.Length;
        var end = html.IndexOf('"', start);
        Assert.True(end > start, "Anti-forgery token value was not rendered.");
        return html[start..end];
    }
}
