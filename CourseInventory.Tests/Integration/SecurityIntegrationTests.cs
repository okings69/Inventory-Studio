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
}
