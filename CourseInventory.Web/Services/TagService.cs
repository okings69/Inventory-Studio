using CourseInventory.Web.Data;
using CourseInventory.Web.Models.Inventory;
using Microsoft.EntityFrameworkCore;

namespace CourseInventory.Web.Services;

public interface ITagService
{
    Task SetTagsAsync(int inventoryId, string? csv);
    Task<IReadOnlyList<string>> AutocompleteAsync(string term);
    string Normalize(string value);
}

public class TagService(ApplicationDbContext db) : ITagService
{
    public string Normalize(string value) => value.Trim().ToUpperInvariant();

    public async Task SetTagsAsync(int inventoryId, string? csv)
    {
        var names = (csv ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => t.Length is > 0 and <= 60)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToList();
        var normalizedNames = names.Select(Normalize).ToArray();

        var current = await db.InventoryTags.Where(t => t.InventoryId == inventoryId).ToListAsync();
        db.InventoryTags.RemoveRange(current);

        var existingTags = normalizedNames.Length == 0
            ? new Dictionary<string, Tag>(StringComparer.OrdinalIgnoreCase)
            : await db.Tags
                .Where(t => normalizedNames.Contains(t.NormalizedName))
                .ToDictionaryAsync(t => t.NormalizedName, StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < names.Count; i++)
        {
            var name = names[i];
            var normalized = normalizedNames[i];
            if (!existingTags.TryGetValue(normalized, out var tag))
            {
                tag = new Tag { Name = name, NormalizedName = normalized };
                db.Tags.Add(tag);
                existingTags[normalized] = tag;
            }
            db.InventoryTags.Add(new InventoryTag { InventoryId = inventoryId, Tag = tag });
        }
        await db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<string>> AutocompleteAsync(string term)
    {
        var normalized = Normalize(term);
        return await db.Tags.AsNoTracking()
            .Where(t => t.NormalizedName.StartsWith(normalized))
            .OrderBy(t => t.Name)
            .Select(t => t.Name)
            .Take(12)
            .ToListAsync();
    }
}
