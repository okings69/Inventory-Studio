using CourseInventory.Web.Data;
using CourseInventory.Web.Models;
using CourseInventory.Web.Models.Inventory;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CourseInventory.Web.Services;

public interface IItemService
{
    Task<InventoryItem?> GetAsync(int id);
    Task<ServiceResult> CreateAsync(InventoryItem item, ApplicationUser actor);
    Task<ServiceResult> UpdateAsync(InventoryItem item, ApplicationUser actor);
    Task<ServiceResult> DeleteManyAsync(int inventoryId, int[] ids, ApplicationUser actor);
    Task<ServiceResult> ToggleLikeAsync(int itemId, ApplicationUser actor);
}

public class ItemService(ApplicationDbContext db, IAccessService accessService, ICustomIdService customIds) : IItemService
{
    public Task<InventoryItem?> GetAsync(int id) =>
        db.InventoryItems.Include(i => i.Inventory).Include(i => i.Likes).FirstOrDefaultAsync(i => i.Id == id);

    public async Task<ServiceResult> CreateAsync(InventoryItem item, ApplicationUser actor)
    {
        var access = await accessService.GetAccessAsync(item.InventoryId, actor);
        if (!access.CanWrite) return ServiceResult.Fail("Access denied.");
        var validation = await ValidateBusinessFieldsAsync(item);
        if (!validation.Success) return validation;
        var customIdResult = await ResolveCustomIdAsync(item.InventoryId, item.CustomId);
        if (!customIdResult.Success) return ServiceResult.Fail(customIdResult.Error ?? "Custom ID generation failed.");

        item.CustomId = customIdResult.Value;
        item.CreatedById = actor.Id;
        item.CreatedAt = item.UpdatedAt = DateTime.UtcNow;
        db.InventoryItems.Add(item);
        return await SaveWithUniqueCustomIdMessage();
    }

    public async Task<ServiceResult> UpdateAsync(InventoryItem item, ApplicationUser actor)
    {
        var current = await db.InventoryItems.FirstOrDefaultAsync(i => i.Id == item.Id);
        if (current is null) return ServiceResult.Fail("Item not found.");
        var access = await accessService.GetAccessAsync(current.InventoryId, actor);
        if (!access.CanWrite) return ServiceResult.Fail("Access denied.");
        item.InventoryId = current.InventoryId;
        var validation = await ValidateBusinessFieldsAsync(item);
        if (!validation.Success) return validation;

        db.Entry(current).Property(i => i.RowVersion).OriginalValue = item.RowVersion;
        var customIdResult = await ResolveCustomIdAsync(current.InventoryId, item.CustomId);
        if (!customIdResult.Success) return ServiceResult.Fail(customIdResult.Error ?? "Custom ID generation failed.");

        current.CustomId = customIdResult.Value;
        current.Text1 = item.Text1; current.Text2 = item.Text2; current.Text3 = item.Text3;
        current.LongText1 = item.LongText1; current.LongText2 = item.LongText2; current.LongText3 = item.LongText3;
        current.Number1 = item.Number1; current.Number2 = item.Number2; current.Number3 = item.Number3;
        current.Link1 = item.Link1; current.Link2 = item.Link2; current.Link3 = item.Link3;
        current.Bool1 = item.Bool1; current.Bool2 = item.Bool2; current.Bool3 = item.Bool3;
        current.UpdatedAt = DateTime.UtcNow;

        try
        {
            return await SaveWithUniqueCustomIdMessage();
        }
        catch (DbUpdateConcurrencyException)
        {
            return ServiceResult.Fail("Cet élément a été modifié par un autre utilisateur. Rechargez la page avant de continuer.");
        }
    }

    public async Task<ServiceResult> DeleteManyAsync(int inventoryId, int[] ids, ApplicationUser actor)
    {
        var access = await accessService.GetAccessAsync(inventoryId, actor);
        if (!access.CanWrite) return ServiceResult.Fail("Access denied.");
        var rows = await db.InventoryItems.Where(i => i.InventoryId == inventoryId && ids.Contains(i.Id)).ToListAsync();
        db.InventoryItems.RemoveRange(rows);
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> ToggleLikeAsync(int itemId, ApplicationUser actor)
    {
        var item = await db.InventoryItems.AsNoTracking().FirstOrDefaultAsync(i => i.Id == itemId);
        if (item is null) return ServiceResult.Fail("Item not found.");
        var existing = await db.ItemLikes.FirstOrDefaultAsync(l => l.ItemId == itemId && l.UserId == actor.Id);
        if (existing is null) db.ItemLikes.Add(new ItemLike { ItemId = itemId, UserId = actor.Id });
        else db.ItemLikes.Remove(existing);
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    private async Task<ValueServiceResult<string>> ResolveCustomIdAsync(int inventoryId, string? customId)
    {
        if (!string.IsNullOrWhiteSpace(customId))
        {
            return ValueServiceResult<string>.Ok(customId.Trim());
        }

        try
        {
            var generatedCustomId = await customIds.GenerateAsync(inventoryId);

            if (string.IsNullOrWhiteSpace(generatedCustomId))
            {
                return ValueServiceResult<string>.Fail("Custom ID generation failed.");
            }

            return ValueServiceResult<string>.Ok(generatedCustomId);
        }
        catch (Exception)
        {
            return ValueServiceResult<string>.Fail("Custom ID generation failed.");
        }
    }

    private async Task<ServiceResult> SaveWithUniqueCustomIdMessage()
    {
        try
        {
            await db.SaveChangesAsync();
            return ServiceResult.Ok();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return ServiceResult.Fail("This Custom ID already exists in this inventory.");
        }
    }

    private async Task<ServiceResult> ValidateBusinessFieldsAsync(InventoryItem item)
    {
        var inventory = await db.Inventories.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == item.InventoryId);
        if (inventory is null) return ServiceResult.Fail("Inventory not found.");

        var fields = await db.InventoryFields.AsNoTracking()
            .Where(f => f.InventoryId == item.InventoryId)
            .ToListAsync();

        var modelField = FindField(fields, "model");
        var statusField = FindField(fields, "status");
        var yearField = FindField(fields, "year");
        var serialField = FindField(fields, "serialnumber");

        if (modelField is not null && string.IsNullOrWhiteSpace(GetStringValue(item, modelField.FieldKey)))
            return ServiceResult.Fail("Model is required.");

        if (serialField is not null)
        {
            var serial = GetStringValue(item, serialField.FieldKey);
            if (string.IsNullOrWhiteSpace(serial)) return ServiceResult.Fail("Serial Number is required.");

            var existingItems = await db.InventoryItems.AsNoTracking()
                .Where(i => i.InventoryId == item.InventoryId && i.Id != item.Id)
                .ToListAsync();
            var duplicate = existingItems.Any(i =>
                string.Equals(GetSerialComparable(i, serialField.FieldKey), serial, StringComparison.OrdinalIgnoreCase));
            if (duplicate) return ServiceResult.Fail("Serial Number already exists in this inventory.");
        }

        if (yearField is not null)
        {
            var year = GetNumberValue(item, yearField.FieldKey);
            var currentYear = DateTime.UtcNow.Year;
            if (!year.HasValue) return ServiceResult.Fail("Year is required.");
            if (year.Value < 2000 || year.Value > currentYear) return ServiceResult.Fail($"Year must be between 2000 and {currentYear}.");
        }

        if (statusField is not null)
        {
            var status = GetStringValue(item, statusField.FieldKey);
            var allowed = ParseStatusOptions(inventory.StatusOptions);
            if (string.IsNullOrWhiteSpace(status))
                return ServiceResult.Fail("Status is required.");
            if (allowed.Count > 0 && !allowed.Contains(status, StringComparer.OrdinalIgnoreCase))
                return ServiceResult.Fail($"Status must be one of: {string.Join(", ", allowed)}.");
        }

        return ServiceResult.Ok();
    }

    private static InventoryField? FindField(IEnumerable<InventoryField> fields, string normalizedTitle) =>
        fields.FirstOrDefault(f => Normalize(f.Title) == normalizedTitle);

    private static string Normalize(string value) =>
        new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static string? GetStringValue(InventoryItem item, string key) => key switch
    {
        "Text1" => item.Text1, "Text2" => item.Text2, "Text3" => item.Text3,
        "LongText1" => item.LongText1, "LongText2" => item.LongText2, "LongText3" => item.LongText3,
        "Link1" => item.Link1, "Link2" => item.Link2, "Link3" => item.Link3,
        _ => null
    };

    private static decimal? GetNumberValue(InventoryItem item, string key) => key switch
    {
        "Number1" => item.Number1, "Number2" => item.Number2, "Number3" => item.Number3,
        _ => null
    };

    private static string? GetSerialComparable(InventoryItem item, string key) => GetStringValue(item, key);

    private static IReadOnlyList<string> ParseStatusOptions(string? statusOptions) =>
        (string.IsNullOrWhiteSpace(statusOptions) ? Inventory.DefaultStatusOptions : statusOptions)
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
}
