using Markdig;
using System.Text.RegularExpressions;

namespace CourseInventory.Web.Services;

public interface IMarkdownService
{
    string ToHtml(string markdown);
}

public class MarkdownService : IMarkdownService
{
    private static readonly Regex UnsafeUrlAttribute = new(
        "\\s(?<name>href|src)=\"(?<value>[^\"]*)\"",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    private static readonly Regex AnchorWithoutRel = new(
        "<a\\s+(?![^>]*\\brel=)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    private readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .DisableHtml()
        .Build();

    public string ToHtml(string markdown)
    {
        var html = Markdown.ToHtml(markdown ?? string.Empty, _pipeline);
        html = UnsafeUrlAttribute.Replace(html, match =>
        {
            var name = match.Groups["name"].Value.ToLowerInvariant();
            var value = match.Groups["value"].Value;
            return IsSafeMarkdownUrl(value)
                ? $" {name}=\"{value}\""
                : $" {name}=\"#\"";
        });

        return AnchorWithoutRel.Replace(html, "<a rel=\"nofollow noopener noreferrer\" ");
    }

    private static bool IsSafeMarkdownUrl(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (value.StartsWith('#') || value.StartsWith('/'))
        {
            return true;
        }

        return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            uri.Scheme is "http" or "https" or "mailto";
    }
}
