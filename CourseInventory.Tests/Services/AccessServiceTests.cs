using CourseInventory.Tests.TestSupport;
using CourseInventory.Web.Models.Inventory;

namespace CourseInventory.Tests.Services;

public class AccessServiceTests
{
    [Fact]
    public async Task Visitor_CanOnlyReadPublicInventory()
    {
        await using var db = TestDb.CreateContext();
        var users = TestDb.CreateUserManager(db);
        var owner = await TestDb.AddUserAsync(db, users, "owner");
        var service = TestDb.CreateAccessService(db, users);

        db.Inventories.AddRange(
            new Inventory { Id = 1, Title = "Public", Category = "General", OwnerId = owner.Id, IsPublic = true },
            new Inventory { Id = 2, Title = "Private", Category = "General", OwnerId = owner.Id, IsPublic = false });
        await db.SaveChangesAsync();

        var publicAccess = await service.GetAccessAsync(1, null);
        var privateAccess = await service.GetAccessAsync(2, null);

        Assert.True(publicAccess.CanRead);
        Assert.False(publicAccess.CanWrite);
        Assert.False(publicAccess.CanManage);
        Assert.False(privateAccess.CanRead);
    }

    [Fact]
    public async Task Owner_CanReadWriteAndManage()
    {
        await using var db = TestDb.CreateContext();
        var users = TestDb.CreateUserManager(db);
        var owner = await TestDb.AddUserAsync(db, users, "owner");
        var service = TestDb.CreateAccessService(db, users);

        db.Inventories.Add(new Inventory { Id = 1, Title = "Private", Category = "General", OwnerId = owner.Id });
        await db.SaveChangesAsync();

        var access = await service.GetAccessAsync(1, owner);

        Assert.True(access.CanRead);
        Assert.True(access.CanWrite);
        Assert.True(access.CanManage);
    }

    [Fact]
    public async Task ExplicitAccess_CanReadAndWriteButCannotManage()
    {
        await using var db = TestDb.CreateContext();
        var users = TestDb.CreateUserManager(db);
        var owner = await TestDb.AddUserAsync(db, users, "owner");
        var shared = await TestDb.AddUserAsync(db, users, "shared");
        var service = TestDb.CreateAccessService(db, users);

        db.Inventories.Add(new Inventory { Id = 1, Title = "Private", Category = "General", OwnerId = owner.Id });
        db.InventoryAccesses.Add(new InventoryAccess { InventoryId = 1, UserId = shared.Id });
        await db.SaveChangesAsync();

        var access = await service.GetAccessAsync(1, shared);

        Assert.True(access.CanRead);
        Assert.True(access.CanWrite);
        Assert.False(access.CanManage);
    }

    [Fact]
    public async Task Admin_HasFullOverride()
    {
        await using var db = TestDb.CreateContext();
        var users = TestDb.CreateUserManager(db);
        var owner = await TestDb.AddUserAsync(db, users, "owner");
        var admin = await TestDb.AddUserAsync(db, users, "admin", isAdmin: true);
        var service = TestDb.CreateAccessService(db, users);

        db.Inventories.Add(new Inventory { Id = 1, Title = "Private", Category = "General", OwnerId = owner.Id });
        await db.SaveChangesAsync();

        var access = await service.GetAccessAsync(1, admin);

        Assert.True(access.CanRead);
        Assert.True(access.CanWrite);
        Assert.True(access.CanManage);
        Assert.True(access.IsAdmin);
    }
}
