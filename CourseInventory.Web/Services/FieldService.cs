using CourseInventory.Web.Data;
using CourseInventory.Web.Models.Inventory;
using Microsoft.EntityFrameworkCore;

namespace CourseInventory.Web.Services;

public interface IFieldService
{
    Task<ServiceResult> AddAsync(int inventoryId, InventoryFieldType type, string title, string? description);
    Task<ServiceResult> UpdateAsync(InventoryField field);
    Task<ServiceResult> DeleteAsync(int fieldId);
    string NextFieldKey(InventoryFieldType type, IEnumerable<InventoryField> existing);
}

public class FieldService(ApplicationDbContext db) : IFieldService
{
    private static readonly Dictionary<InventoryFieldType, string[]> Slots = new()
    {
        [InventoryFieldType.SingleLineText] = ["Text1", "Text2", "Text3"],
        [InventoryFieldType.MultiLineText] = ["LongText1", "LongText2", "LongText3"],
        [InventoryFieldType.Number] = ["Number1", "Number2", "Number3"],
        [InventoryFieldType.Link] = ["Link1", "Link2", "Link3"],
        [InventoryFieldType.Boolean] = ["Bool1", "Bool2", "Bool3"]
    };

    public async Task<ServiceResult> AddAsync(int inventoryId, InventoryFieldType type, string title, string? description)
    {
        var existing = await db.InventoryFields.Where(f => f.InventoryId == inventoryId).ToListAsync();
        var key = NextFieldKey(type, existing);
        if (key.Length == 0) return ServiceResult.Fail("Only three fields of this type are allowed.");
        db.InventoryFields.Add(new InventoryField
        {
            InventoryId = inventoryId,
            FieldType = type,
            FieldKey = key,
            Title = title.Trim(),
            Description = description,
            SortOrder = existing.Count == 0 ? 10 : existing.Max(f => f.SortOrder) + 10
        });
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> UpdateAsync(InventoryField field)
    {
        var current = await db.InventoryFields.FindAsync(field.Id);
        if (current is null) return ServiceResult.Fail("Field not found.");
        current.Title = field.Title.Trim();
        current.Description = field.Description;
        current.ShowInTable = field.ShowInTable;
        current.SortOrder = field.SortOrder;
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> DeleteAsync(int fieldId)
    {
        var field = await db.InventoryFields.FindAsync(fieldId);
        if (field is null) return ServiceResult.Fail("Field not found.");
        db.InventoryFields.Remove(field);
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public string NextFieldKey(InventoryFieldType type, IEnumerable<InventoryField> existing)
    {
        var used = existing.Where(f => f.FieldType == type).Select(f => f.FieldKey).ToHashSet();
        return Slots[type].FirstOrDefault(slot => !used.Contains(slot)) ?? string.Empty;
    }
}
