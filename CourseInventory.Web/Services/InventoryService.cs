using CourseInventory.Web.Data;
using CourseInventory.Web.Models;
using CourseInventory.Web.Models.Inventory;
using Microsoft.EntityFrameworkCore;

namespace CourseInventory.Web.Services;

public interface IInventoryService
{
    Task<IReadOnlyList<Inventory>> LatestAsync(int count = 12);
    Task<IReadOnlyList<Inventory>> PopularAsync(int count = 5);
    Task<Inventory?> GetDetailsAsync(int id);
    Task<Inventory> CreateAsync(Inventory inventory, ApplicationUser owner, string? tags);
    Task<ServiceResult> UpdateSettingsAsync(Inventory update, string? tags, ApplicationUser actor);
    Task<ServiceResult> DeleteAsync(int id, ApplicationUser actor);
}

public class InventoryService(ApplicationDbContext db, IAccessService accessService, ITagService tagService) : IInventoryService
{
    public async Task<IReadOnlyList<Inventory>> LatestAsync(int count = 12)
    {
        var inventories = await db.Inventories.AsNoTracking()
            .Include(i => i.Owner)
            .Include(i => i.InventoryTags).ThenInclude(t => t.Tag)
            .OrderByDescending(i => i.CreatedAt)
            .Take(count)
            .ToListAsync();
        await ApplyItemCountsAsync(inventories);
        return inventories;
    }

    public async Task<IReadOnlyList<Inventory>> PopularAsync(int count = 5)
    {
        var inventories = await db.Inventories.AsNoTracking()
            .Include(i => i.Owner)
            .OrderByDescending(i => i.Items.Count)
            .ThenByDescending(i => i.UpdatedAt)
            .Take(count)
            .ToListAsync();
        await ApplyItemCountsAsync(inventories);
        return inventories;
    }

    public async Task<Inventory?> GetDetailsAsync(int id) =>
        await db.Inventories
            .Include(i => i.Owner)
            .Include(i => i.Items).ThenInclude(item => item.Likes)
            .Include(i => i.Fields.OrderBy(f => f.SortOrder))
            .Include(i => i.CustomIdElements.OrderBy(e => e.SortOrder))
            .Include(i => i.Accesses).ThenInclude(a => a.User)
            .Include(i => i.Messages.OrderByDescending(m => m.CreatedAt).Take(50)).ThenInclude(m => m.Author)
            .Include(i => i.InventoryTags).ThenInclude(t => t.Tag)
            .FirstOrDefaultAsync(i => i.Id == id);

    public async Task<Inventory> CreateAsync(Inventory inventory, ApplicationUser owner, string? tags)
    {
        inventory.OwnerId = owner.Id;
        inventory.StatusOptions = NormalizeStatusOptions(inventory.StatusOptions);
        inventory.CreatedAt = inventory.UpdatedAt = DateTime.UtcNow;
        db.Inventories.Add(inventory);
        await db.SaveChangesAsync();
        await tagService.SetTagsAsync(inventory.Id, tags);
        db.CustomIdElements.AddRange(
            new CustomIdElement { InventoryId = inventory.Id, ElementType = CustomIdElementType.FixedText, FixedValue = "ITEM-", SortOrder = 10 },
            new CustomIdElement { InventoryId = inventory.Id, ElementType = CustomIdElementType.Sequence, Format = "D5", SortOrder = 20 });
        await db.SaveChangesAsync();
        return inventory;
    }

    public async Task<ServiceResult> UpdateSettingsAsync(Inventory update, string? tags, ApplicationUser actor)
    {
        var access = await accessService.GetAccessAsync(update.Id, actor);
        if (!access.CanManage) return ServiceResult.Fail("Access denied.");

        var current = await db.Inventories.FirstOrDefaultAsync(i => i.Id == update.Id);
        if (current is null) return ServiceResult.Fail("Inventory not found.");

        db.Entry(current).Property(i => i.RowVersion).OriginalValue = update.RowVersion;
        current.Title = update.Title.Trim();
        current.DescriptionMarkdown = update.DescriptionMarkdown ?? string.Empty;
        current.Category = update.Category.Trim();
        current.ImageUrl = string.IsNullOrWhiteSpace(update.ImageUrl) ? null : update.ImageUrl.Trim();
        current.StatusOptions = NormalizeStatusOptions(update.StatusOptions);
        current.IsPublic = update.IsPublic;
        current.UpdatedAt = DateTime.UtcNow;

        try
        {
            await db.SaveChangesAsync();
            await tagService.SetTagsAsync(current.Id, tags);
            return ServiceResult.Ok();
        }
        catch (DbUpdateConcurrencyException)
        {
            return ServiceResult.Fail("Cet élément a été modifié par un autre utilisateur. Rechargez la page avant de continuer.");
        }
    }

    public async Task<ServiceResult> DeleteAsync(int id, ApplicationUser actor)
    {
        var access = await accessService.GetAccessAsync(id, actor);
        if (!access.CanManage) return ServiceResult.Fail("Access denied.");
        var inventory = await db.Inventories.FindAsync(id);
        if (inventory is null) return ServiceResult.Fail("Inventory not found.");
        db.Inventories.Remove(inventory);
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    private static string NormalizeStatusOptions(string? statusOptions)
    {
        var values = statusOptions?
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return values is { Length: > 0 }
            ? string.Join(", ", values)
            : Inventory.DefaultStatusOptions;
    }

    private async Task ApplyItemCountsAsync(IReadOnlyList<Inventory> inventories)
    {
        if (inventories.Count == 0)
        {
            return;
        }

        var inventoryIds = inventories.Select(i => i.Id).ToArray();
        var counts = await db.InventoryItems.AsNoTracking()
            .Where(item => inventoryIds.Contains(item.InventoryId))
            .GroupBy(item => item.InventoryId)
            .Select(group => new { InventoryId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.InventoryId, row => row.Count);

        foreach (var inventory in inventories)
        {
            inventory.ItemCount = counts.GetValueOrDefault(inventory.Id);
        }
    }
}
