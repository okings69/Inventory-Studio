using Markdig;

namespace CourseInventory.Web.Services;

public interface IMarkdownService
{
    string ToHtml(string markdown);
}

public class MarkdownService : IMarkdownService
{
    private readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .DisableHtml()
        .Build();

    public string ToHtml(string markdown) => Markdown.ToHtml(markdown ?? string.Empty, _pipeline);
}
