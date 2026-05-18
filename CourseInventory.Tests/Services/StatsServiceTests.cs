using CourseInventory.Tests.TestSupport;
using CourseInventory.Web.Models.Inventory;
using CourseInventory.Web.Services;

namespace CourseInventory.Tests.Services;

public class StatsServiceTests
{
    [Fact]
    public async Task BuildAsync_ReturnsItemLikesAndNumberAggregates()
    {
        await using var db = TestDb.CreateContext();
        db.InventoryItems.AddRange(
            new InventoryItem { Id = 1, InventoryId = 10, CreatedById = "u1", Number1 = 10, Text1 = "Router" },
            new InventoryItem { Id = 2, InventoryId = 10, CreatedById = "u1", Number1 = 30, Text1 = "Router" },
            new InventoryItem { Id = 3, InventoryId = 10, CreatedById = "u1", Number1 = 20, Text1 = "Laptop" });
        db.ItemLikes.AddRange(
            new ItemLike { ItemId = 1, UserId = "u1" },
            new ItemLike { ItemId = 2, UserId = "u2" });
        await db.SaveChangesAsync();

        var service = new StatsService(db);

        var stats = await service.BuildAsync(10);

        Assert.Equal(3, stats.ItemCount);
        Assert.Equal(2, stats.TotalLikes);
        Assert.Equal(10, stats.NumberStats["Number1"].Min);
        Assert.Equal(30, stats.NumberStats["Number1"].Max);
        Assert.Equal(20, stats.NumberStats["Number1"].Average);
        Assert.Equal("Router", stats.FrequentTextValues["Text1"].Values.First().Value);
        Assert.Equal(2, stats.FrequentTextValues["Text1"].Values.First().Count);
    }

    [Fact]
    public async Task BuildAsync_UsesConfiguredFieldTitles()
    {
        await using var db = TestDb.CreateContext();
        db.InventoryFields.AddRange(
            new InventoryField
            {
                InventoryId = 20,
                FieldType = InventoryFieldType.SingleLineText,
                FieldKey = "Text1",
                Title = "Brand",
                SortOrder = 10
            },
            new InventoryField
            {
                InventoryId = 20,
                FieldType = InventoryFieldType.Number,
                FieldKey = "Number1",
                Title = "RAM Size (GB)",
                SortOrder = 20
            });
        db.InventoryItems.AddRange(
            new InventoryItem { Id = 10, InventoryId = 20, CreatedById = "u1", Number1 = 16, Text1 = "Lenovo" },
            new InventoryItem { Id = 11, InventoryId = 20, CreatedById = "u1", Number1 = 32, Text1 = "Lenovo" });
        await db.SaveChangesAsync();

        var service = new StatsService(db);

        var stats = await service.BuildAsync(20);

        Assert.Equal("RAM Size (GB)", stats.NumberStats["Number1"].Label);
        Assert.Equal("Brand", stats.FrequentTextValues["Text1"].Label);
        Assert.Equal(24, stats.NumberStats["Number1"].Average);
    }
}
