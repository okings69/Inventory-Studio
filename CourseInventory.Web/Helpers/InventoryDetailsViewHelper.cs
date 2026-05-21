using CourseInventory.Web.Models.Inventory;

namespace CourseInventory.Web.Helpers;

public static class InventoryDetailsViewHelper
{
    public static object? GetFieldValue(InventoryItem item, string key) => key switch
    {
        "Text1" => item.Text1,
        "Text2" => item.Text2,
        "Text3" => item.Text3,
        "Number1" => item.Number1,
        "Number2" => item.Number2,
        "Number3" => item.Number3,
        "LongText1" => item.LongText1,
        "LongText2" => item.LongText2,
        "LongText3" => item.LongText3,
        "Link1" => item.Link1,
        "Link2" => item.Link2,
        "Link3" => item.Link3,
        "Bool1" => item.Bool1,
        "Bool2" => item.Bool2,
        "Bool3" => item.Bool3,
        _ => null
    };

    public static string FormatFieldValue(object? value) => value switch
    {
        null => "-",
        string text when string.IsNullOrWhiteSpace(text) => "-",
        decimal number => number.ToString("0.##"),
        bool boolean => boolean ? "Yes" : "No",
        _ => value.ToString() ?? "-"
    };

    public static string FormatStatsNumber(decimal? value) =>
        value.HasValue ? value.Value.ToString("0.##") : "-";

    public static string GetStatusBadgeClass(InventoryField field, object? value)
    {
        if (!IsStatusField(field))
        {
            return string.Empty;
        }

        var status = value?.ToString()?.Trim();

        return status switch
        {
            "Available" => "status-available",
            "In use" => "status-in-use",
            "Broken" => "status-broken",
            _ => "status-unknown"
        };
    }

    public static bool IsStatusField(InventoryField field) =>
        string.Equals(field.Title?.Trim(), "Status", StringComparison.OrdinalIgnoreCase);

    public static string NormalizeFieldTitle(string? value) =>
        new((value ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    public static string TrimDuplicateHeading(string title, string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return string.Empty;
        }

        var lines = markdown.Replace("\r\n", "\n").Split('\n').ToList();
        var firstContentIndex = lines.FindIndex(line => !string.IsNullOrWhiteSpace(line));
        if (firstContentIndex < 0)
        {
            return markdown;
        }

        var normalizedFirstLine = lines[firstContentIndex].Trim().TrimStart('#', ' ').Trim();
        if (!string.Equals(normalizedFirstLine, title?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return markdown;
        }

        lines.RemoveAt(firstContentIndex);
        while (firstContentIndex < lines.Count && string.IsNullOrWhiteSpace(lines[firstContentIndex]))
        {
            lines.RemoveAt(firstContentIndex);
        }

        return string.Join('\n', lines);
    }
}
