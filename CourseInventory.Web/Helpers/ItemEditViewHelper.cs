using Microsoft.AspNetCore.Html;

namespace CourseInventory.Web.Helpers;

public static class ItemEditViewHelper
{
    public static string NormalizeTitle(string value) =>
        new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    public static IReadOnlyList<string> ParseStatusOptions(string? statusOptions) =>
        (string.IsNullOrWhiteSpace(statusOptions)
            ? Models.Inventory.Inventory.DefaultStatusOptions
            : statusOptions)
        .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public static IHtmlContent RenderFieldValidation(string fieldKey) => fieldKey switch
    {
        "Text1" => ValidationFor("Item.Text1"),
        "Text2" => ValidationFor("Item.Text2"),
        "Text3" => ValidationFor("Item.Text3"),
        "LongText1" => ValidationFor("Item.LongText1"),
        "LongText2" => ValidationFor("Item.LongText2"),
        "LongText3" => ValidationFor("Item.LongText3"),
        "Number1" => ValidationFor("Item.Number1"),
        "Number2" => ValidationFor("Item.Number2"),
        "Number3" => ValidationFor("Item.Number3"),
        "Link1" => ValidationFor("Item.Link1"),
        "Link2" => ValidationFor("Item.Link2"),
        "Link3" => ValidationFor("Item.Link3"),
        "Bool1" => ValidationFor("Item.Bool1"),
        "Bool2" => ValidationFor("Item.Bool2"),
        "Bool3" => ValidationFor("Item.Bool3"),
        _ => HtmlString.Empty
    };

    private static HtmlString ValidationFor(string fieldName) =>
        new($"""<span class="text-danger field-validation-valid" data-valmsg-for="{fieldName}" data-valmsg-replace="true"></span>""");
}
