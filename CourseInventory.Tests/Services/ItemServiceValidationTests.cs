using CourseInventory.Tests.TestSupport;
using CourseInventory.Web.Models.Inventory;
using CourseInventory.Web.Services;

namespace CourseInventory.Tests.Services;

public class ItemServiceValidationTests
{
    [Fact]
    public async Task CreateAsync_RejectsMissingRequiredSerialNumber()
    {
        await using var db = TestDb.CreateContext();
        var users = TestDb.CreateUserManager(db);
        var owner = await TestDb.AddUserAsync(db, users, "owner");
        var service = CreateItemService(db, users);

        db.Inventories.Add(new Inventory { Id = 1, Title = "Devices", Category = "IT", OwnerId = owner.Id });
        db.InventoryFields.Add(new InventoryField
        {
            InventoryId = 1,
            FieldType = InventoryFieldType.SingleLineText,
            FieldKey = "Text1",
            Title = "Serial Number"
        });
        await db.SaveChangesAsync();

        var result = await service.CreateAsync(new InventoryItem { InventoryId = 1 }, owner);

        Assert.False(result.Success);
        Assert.Equal("Serial Number is required.", result.Error);
    }

    [Fact]
    public async Task CreateAsync_RejectsDuplicateSerialNumber()
    {
        await using var db = TestDb.CreateContext();
        var users = TestDb.CreateUserManager(db);
        var owner = await TestDb.AddUserAsync(db, users, "owner");
        var service = CreateItemService(db, users);

        db.Inventories.Add(new Inventory { Id = 1, Title = "Devices", Category = "IT", OwnerId = owner.Id });
        db.InventoryFields.Add(new InventoryField
        {
            InventoryId = 1,
            FieldType = InventoryFieldType.SingleLineText,
            FieldKey = "Text1",
            Title = "Serial Number"
        });
        db.InventoryItems.Add(new InventoryItem
        {
            InventoryId = 1,
            CreatedById = owner.Id,
            CustomId = "EXISTING",
            Text1 = "ABC-123"
        });
        await db.SaveChangesAsync();

        var result = await service.CreateAsync(new InventoryItem { InventoryId = 1, CustomId = "NEW", Text1 = "abc-123" }, owner);

        Assert.False(result.Success);
        Assert.Equal("Serial Number already exists in this inventory.", result.Error);
    }

    [Fact]
    public async Task CreateAsync_AcceptsAllowedStatusOption()
    {
        await using var db = TestDb.CreateContext();
        var users = TestDb.CreateUserManager(db);
        var owner = await TestDb.AddUserAsync(db, users, "owner");
        var service = CreateItemService(db, users);

        db.Inventories.Add(new Inventory
        {
            Id = 1,
            Title = "Books",
            Category = "Library",
            OwnerId = owner.Id,
            StatusOptions = "Available, Borrowed"
        });
        db.InventoryFields.Add(new InventoryField
        {
            InventoryId = 1,
            FieldType = InventoryFieldType.SingleLineText,
            FieldKey = "Text1",
            Title = "Status"
        });
        await db.SaveChangesAsync();

        var result = await service.CreateAsync(new InventoryItem { InventoryId = 1, CustomId = "BOOK-1", Text1 = "Available" }, owner);

        Assert.True(result.Success);
    }

    private static ItemService CreateItemService(
        CourseInventory.Web.Data.ApplicationDbContext db,
        Microsoft.AspNetCore.Identity.UserManager<CourseInventory.Web.Models.ApplicationUser> users)
    {
        var access = TestDb.CreateAccessService(db, users);
        return new ItemService(db, access, new CustomIdService(db));
    }
}
