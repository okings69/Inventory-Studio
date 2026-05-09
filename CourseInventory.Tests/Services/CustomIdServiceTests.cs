using CourseInventory.Tests.TestSupport;
using CourseInventory.Web.Models.Inventory;
using CourseInventory.Web.Services;

namespace CourseInventory.Tests.Services;

public class CustomIdServiceTests
{
    [Fact]
    public async Task Preview_UsesDefaultPattern_WhenNoElementsAreConfigured()
    {
        await using var db = TestDb.CreateContext();
        var service = new CustomIdService(db);

        var preview = await service.PreviewAsync(1);

        Assert.Equal("ITEM-00001", preview);
    }

    [Fact]
    public async Task Generate_UsesFixedTextAndSequence()
    {
        await using var db = TestDb.CreateContext();
        db.CustomIdElements.AddRange(
            new CustomIdElement { InventoryId = 1, ElementType = CustomIdElementType.FixedText, FixedValue = "IT-", SortOrder = 1 },
            new CustomIdElement { InventoryId = 1, ElementType = CustomIdElementType.Sequence, Format = "D3", SortOrder = 2 });
        await db.SaveChangesAsync();

        var service = new CustomIdService(db);

        var value = await service.GenerateAsync(1);

        Assert.Equal("IT-001", value);
    }

    [Fact]
    public async Task ValidateElements_RejectsEmptyFixedText()
    {
        await using var db = TestDb.CreateContext();
        var service = new CustomIdService(db);

        var result = await service.ValidateElementsAsync([
            new CustomIdElement { ElementType = CustomIdElementType.FixedText }
        ]);

        Assert.False(result.Success);
        Assert.Equal("Fixed text elements need a value.", result.Error);
    }
}
