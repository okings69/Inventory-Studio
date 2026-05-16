using CourseInventory.Web.Services;

namespace CourseInventory.Tests.Services;

public class MarkdownServiceSecurityTests
{
    [Fact]
    public void ToHtml_RemovesRawHtml()
    {
        var service = new MarkdownService();

        var html = service.ToHtml("<script>alert(1)</script>");

        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;script", html);
    }

    [Fact]
    public void ToHtml_ReplacesUnsafeLinkTargets()
    {
        var service = new MarkdownService();

        var html = service.ToHtml("[bad](javascript:alert(1))");

        Assert.DoesNotContain("javascript:", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("href=\"#\"", html);
    }

    [Fact]
    public void ToHtml_AddsSafeExternalLinkRelationship()
    {
        var service = new MarkdownService();

        var html = service.ToHtml("[docs](https://example.com)");

        Assert.Contains("rel=\"nofollow noopener noreferrer\"", html);
        Assert.Contains("href=\"https://example.com\"", html);
    }
}
