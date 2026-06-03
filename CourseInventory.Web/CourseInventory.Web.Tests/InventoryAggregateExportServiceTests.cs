using CourseInventory.Web.Data;
using CourseInventory.Web.Models.Inventory;
using CourseInventory.Web.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CourseInventory.Web.Tests;

public class InventoryAggregateExportServiceTests
{
    [Fact]
    public async Task BuildForTokenAsync_InvalidToken_ReturnsNull()
    {
        await using var db = CreateDb();
        var tokenService = new InventoryApiTokenService(db, TimeProvider.System);
        var service = new InventoryAggregateExportService(db, tokenService);

        var result = await service.BuildForTokenAsync("missing-token");

        Assert.Null(result);
    }

    [Fact]
    public async Task BuildForTokenAsync_ValidToken_ReturnsOnlyMatchingInventory()
    {
        await using var db = CreateDb();
        var tokenService = new InventoryApiTokenService(db, TimeProvider.System);
        var token = "token-one";

        db.Inventories.AddRange(
            new Inventory
            {
                Id = 1,
                Title = "Books 2026",
                Category = "Books",
                OwnerId = "owner",
                ApiTokenHash = tokenService.HashToken(token)
            },
            new Inventory
            {
                Id = 2,
                Title = "Private Other Inventory",
                Category = "Other",
                OwnerId = "owner",
                ApiTokenHash = tokenService.HashToken("token-two")
            });
        db.InventoryFields.Add(new InventoryField
        {
            InventoryId = 1,
            Title = "Year",
            FieldType = InventoryFieldType.Number,
            FieldKey = "Number1",
            SortOrder = 10
        });
        db.InventoryItems.AddRange(
            new InventoryItem { InventoryId = 1, CreatedById = "owner", Number1 = 2024 },
            new InventoryItem { InventoryId = 2, CreatedById = "owner", Number1 = 1999 });
        await db.SaveChangesAsync();

        var service = new InventoryAggregateExportService(db, tokenService);
        var result = await service.BuildForTokenAsync(token);

        Assert.NotNull(result);
        Assert.Equal("Books 2026", result.InventoryTitle);
        Assert.DoesNotContain("Private Other Inventory", result.InventoryTitle);
        Assert.Single(result.NumericAggregates);
        Assert.Equal(2024, result.NumericAggregates[0].Min);
    }

    [Fact]
    public async Task BuildForTokenAsync_BuildsNumericAndTextAggregates()
    {
        await using var db = CreateDb();
        var tokenService = new InventoryApiTokenService(db, TimeProvider.System);
        var token = "aggregate-token";

        db.Inventories.Add(new Inventory
        {
            Id = 1,
            Title = "Medical Equipment Inventory",
            Category = "Medical",
            OwnerId = "owner",
            ApiTokenHash = tokenService.HashToken(token)
        });
        db.InventoryFields.AddRange(
            new InventoryField
            {
                InventoryId = 1,
                Title = "Year",
                FieldType = InventoryFieldType.Number,
                FieldKey = "Number1",
                SortOrder = 10
            },
            new InventoryField
            {
                InventoryId = 1,
                Title = "Status",
                FieldType = InventoryFieldType.SingleLineText,
                FieldKey = "Text1",
                SortOrder = 20
            });
        db.InventoryItems.AddRange(
            new InventoryItem { InventoryId = 1, CreatedById = "owner", Number1 = 1994, Text1 = "Available" },
            new InventoryItem { InventoryId = 1, CreatedById = "owner", Number1 = 2024, Text1 = "Available" },
            new InventoryItem { InventoryId = 1, CreatedById = "owner", Number1 = 2012, Text1 = "Broken" });
        await db.SaveChangesAsync();

        var service = new InventoryAggregateExportService(db, tokenService);
        var result = await service.BuildForTokenAsync(token);

        Assert.NotNull(result);
        Assert.Collection(result.Fields,
            field =>
            {
                Assert.Equal("Year", field.Title);
                Assert.Equal("Number", field.Type);
            },
            field =>
            {
                Assert.Equal("Status", field.Title);
                Assert.Equal("Text", field.Type);
            });

        var numeric = Assert.Single(result.NumericAggregates);
        Assert.Equal("Year", numeric.Field);
        Assert.Equal(1994, numeric.Min);
        Assert.Equal(2024, numeric.Max);
        Assert.Equal(2010, numeric.Average);

        var text = Assert.Single(result.TextAggregates);
        Assert.Equal("Status", text.Field);
        Assert.Equal("Available", text.Values[0].Value);
        Assert.Equal(2, text.Values[0].Count);
        Assert.Equal("Broken", text.Values[1].Value);
        Assert.Equal(1, text.Values[1].Count);
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }
}
