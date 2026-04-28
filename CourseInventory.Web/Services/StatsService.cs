using CourseInventory.Web.Data;
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
        var items = await db.InventoryItems.AsNoTracking()
            .Where(i => i.InventoryId == inventoryId)
            .Include(i => i.Likes)
            .ToListAsync();

        var numberStats = new Dictionary<string, (decimal? Min, decimal? Max, decimal? Average)>
        {
            ["Number1"] = Aggregate(items.Select(i => i.Number1)),
            ["Number2"] = Aggregate(items.Select(i => i.Number2)),
            ["Number3"] = Aggregate(items.Select(i => i.Number3))
        };

        var frequent = new Dictionary<string, IReadOnlyList<(string Value, int Count)>>
        {
            ["Text1"] = Top(items.Select(i => i.Text1)),
            ["Text2"] = Top(items.Select(i => i.Text2)),
            ["Text3"] = Top(items.Select(i => i.Text3))
        };

        return new(items.Count, items.Sum(i => i.Likes.Count), numberStats, frequent);
    }

    private static (decimal? Min, decimal? Max, decimal? Average) Aggregate(IEnumerable<decimal?> values)
    {
        var rows = values.Where(v => v.HasValue).Select(v => v!.Value).ToList();
        return rows.Count == 0 ? (null, null, null) : (rows.Min(), rows.Max(), rows.Average());
    }

    private static IReadOnlyList<(string Value, int Count)> Top(IEnumerable<string?> values) =>
        values.Where(v => !string.IsNullOrWhiteSpace(v))
            .GroupBy(v => v!)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => (g.Key, g.Count()))
            .ToList();
}
