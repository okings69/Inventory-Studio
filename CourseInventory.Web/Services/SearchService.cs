using CourseInventory.Web.Data;
using CourseInventory.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace CourseInventory.Web.Services;

public interface ISearchService
{
    Task<IReadOnlyList<SearchResultItem>> SearchAsync(string query, ApplicationUser? user);
}

public class SearchService(ApplicationDbContext db, IAccessService access) : ISearchService
{
    public async Task<IReadOnlyList<SearchResultItem>> SearchAsync(string query, ApplicationUser? user)
    {
        query = query.Trim();
        if (query.Length < 2) return [];
        var scope = await access.BuildScopeAsync(user);

        // PostgreSQL full text search is used directly here so the database can use GIN indexes created by the migration.
        var inventoryRows = await db.Database.SqlQuery<SearchRow>($"""
            SELECT 'Inventory' AS "Type",
                   i."Id",
                   i."Title",
                   left(coalesce(i."DescriptionMarkdown", ''), 220) AS "Snippet",
                   '/Inventories/Details/' || i."Id" AS "Url"
            FROM "Inventories" i
            LEFT JOIN "InventoryTags" it ON it."InventoryId" = i."Id"
            LEFT JOIN "Tags" t ON t."Id" = it."TagId"
            WHERE to_tsvector('simple', coalesce(i."Title",'') || ' ' || coalesce(i."DescriptionMarkdown",'') || ' ' || coalesce(i."Category",'') || ' ' || coalesce(t."Name",''))
                  @@ plainto_tsquery('simple', {query})
            GROUP BY i."Id"
            ORDER BY i."UpdatedAt" DESC
            LIMIT 20
            """).ToListAsync();

        var itemRows = await db.Database.SqlQuery<SearchRow>($"""
            SELECT 'Item' AS "Type",
                   item."Id",
                   item."CustomId" AS "Title",
                   left(concat_ws(' ', item."Text1", item."Text2", item."Text3", item."LongText1", item."LongText2", item."LongText3"), 220) AS "Snippet",
                   '/Items/Details/' || item."Id" AS "Url"
            FROM "InventoryItems" item
            WHERE to_tsvector('simple', coalesce(item."CustomId",'') || ' ' || coalesce(item."Text1",'') || ' ' || coalesce(item."Text2",'') || ' ' || coalesce(item."Text3",'') || ' ' || coalesce(item."LongText1",'') || ' ' || coalesce(item."LongText2",'') || ' ' || coalesce(item."LongText3",'') || ' ' || coalesce(item."Link1",'') || ' ' || coalesce(item."Link2",'') || ' ' || coalesce(item."Link3",''))
                  @@ plainto_tsquery('simple', {query})
            ORDER BY item."UpdatedAt" DESC
            LIMIT 20
            """).ToListAsync();

        var itemIds = itemRows.Select(r => r.Id).ToArray();
        var itemInventoryRows = itemIds.Length == 0
            ? []
            : await db.InventoryItems.AsNoTracking()
                .Where(item => itemIds.Contains(item.Id))
                .Select(item => new ItemInventoryRow { ItemId = item.Id, InventoryId = item.InventoryId })
                .ToListAsync();

        var candidateInventoryIds = inventoryRows.Select(r => r.Id)
            .Concat(itemInventoryRows.Select(r => r.InventoryId))
            .Distinct()
            .ToArray();

        var readableInventoryIds = candidateInventoryIds.Length == 0
            ? []
            : await access.FilterReadableInventories(
                    db.Inventories.AsNoTracking().Where(i => candidateInventoryIds.Contains(i.Id)),
                    scope)
                .Select(i => i.Id)
                .ToListAsync();

        var readableInventorySet = readableInventoryIds.ToHashSet();
        var itemInventoryMap = itemInventoryRows.ToDictionary(row => row.ItemId, row => row.InventoryId);

        return inventoryRows.Concat(itemRows)
            .Where(r => r.Type == "Inventory"
                ? readableInventorySet.Contains(r.Id)
                : itemInventoryMap.TryGetValue(r.Id, out var inventoryId) && readableInventorySet.Contains(inventoryId))
            .Select(r => new SearchResultItem(r.Type, r.Id, r.Title, r.Snippet, r.Url))
            .ToList();
    }

    private sealed class SearchRow
    {
        public string Type { get; set; } = string.Empty;
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Snippet { get; set; }
        public string Url { get; set; } = string.Empty;
    }

    private sealed class ItemInventoryRow
    {
        public int ItemId { get; set; }
        public int InventoryId { get; set; }
    }
}
