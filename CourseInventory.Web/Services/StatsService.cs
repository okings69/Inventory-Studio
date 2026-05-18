using CourseInventory.Web.Data;
using CourseInventory.Web.Models.Inventory;
using Microsoft.EntityFrameworkCore;

namespace CourseInventory.Web.Services;

public interface IStatsService
{
    Task<InventoryStats> BuildAsync(int inventoryId);
}

public class StatsService(ApplicationDbContext db) : IStatsService
{
    public async Task<InventoryStats> BuildAsync(int inventoryId)
    {
        var fields = await db.InventoryFields.AsNoTracking()
            .Where(f => f.InventoryId == inventoryId)
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.Title)
            .ToListAsync();

        var items = await db.InventoryItems.AsNoTracking()
            .Where(i => i.InventoryId == inventoryId)
            .Include(i => i.Likes)
            .ToListAsync();

        var numberStats = NumberFields(fields)
            .Select(field =>
            {
                var aggregate = Aggregate(items.Select(item => GetNumberValue(item, field.FieldKey)));
                return new NumberFieldStats(field.FieldKey, FieldLabel(field), aggregate.Min, aggregate.Max, aggregate.Average);
            })
            .Where(stat => stat.Min.HasValue || stat.Max.HasValue || stat.Average.HasValue)
            .ToDictionary(stat => stat.FieldKey, stat => stat);

        var frequent = FrequentFields(fields)
            .Select(field => new FrequentFieldStats(
                field.FieldKey,
                FieldLabel(field),
                Top(items.Select(item => FormatFrequentValue(GetFieldValue(item, field.FieldKey))))))
            .Where(stat => stat.Values.Count > 0)
            .ToDictionary(stat => stat.FieldKey, stat => stat);

        return new(items.Count, items.Sum(i => i.Likes.Count), numberStats, frequent);
    }

    private static IReadOnlyList<InventoryField> NumberFields(IReadOnlyList<InventoryField> fields)
    {
        var configured = fields.Where(f => f.FieldType == InventoryFieldType.Number).ToList();
        return configured.Count > 0
            ? configured
            : NumberFieldKeys.Select(key => new InventoryField { FieldKey = key, Title = key, FieldType = InventoryFieldType.Number }).ToList();
    }

    private static IReadOnlyList<InventoryField> FrequentFields(IReadOnlyList<InventoryField> fields)
    {
        var configured = fields.Where(f => f.FieldType != InventoryFieldType.Number).ToList();
        return configured.Count > 0
            ? configured
            : TextFieldKeys.Select(key => new InventoryField { FieldKey = key, Title = key, FieldType = InventoryFieldType.SingleLineText }).ToList();
    }

    private static readonly string[] NumberFieldKeys = ["Number1", "Number2", "Number3"];
    private static readonly string[] TextFieldKeys = ["Text1", "Text2", "Text3"];

    private static string FieldLabel(InventoryField field) =>
        string.IsNullOrWhiteSpace(field.Title) ? field.FieldKey : field.Title.Trim();

    private static decimal? GetNumberValue(InventoryItem item, string fieldKey) => fieldKey switch
    {
        "Number1" => item.Number1,
        "Number2" => item.Number2,
        "Number3" => item.Number3,
        _ => null
    };

    private static object? GetFieldValue(InventoryItem item, string fieldKey) => fieldKey switch
    {
        "Text1" => item.Text1,
        "Text2" => item.Text2,
        "Text3" => item.Text3,
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

    private static string? FormatFrequentValue(object? value) => value switch
    {
        null => null,
        string text when string.IsNullOrWhiteSpace(text) => null,
        string text => text.Trim(),
        bool boolean => boolean ? "Yes" : "No",
        _ => value.ToString()
    };

    private static (decimal? Min, decimal? Max, decimal? Average) Aggregate(IEnumerable<decimal?> values)
    {
        var rows = values.Where(v => v.HasValue).Select(v => v!.Value).ToList();
        return rows.Count == 0 ? (null, null, null) : (rows.Min(), rows.Max(), rows.Average());
    }

    private static IReadOnlyList<FrequentValueStats> Top(IEnumerable<string?> values) =>
        values.Where(v => !string.IsNullOrWhiteSpace(v))
            .GroupBy(v => v!, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .Take(5)
            .Select(g => new FrequentValueStats(g.First()!, g.Count()))
            .ToList();
}
