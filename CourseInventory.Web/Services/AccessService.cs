using CourseInventory.Web.Data;
using CourseInventory.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CourseInventory.Web.Services;

public interface IAccessService
{
    Task<AccessState> GetAccessAsync(int inventoryId, ApplicationUser? user);
    Task<IReadOnlyList<ApplicationUser>> FindUsersAsync(string term);
    Task<ServiceResult> GrantAsync(int inventoryId, string userId, ApplicationUser actor);
    Task<ServiceResult> RevokeAsync(int inventoryId, string userId, ApplicationUser actor);
}

public class AccessService(ApplicationDbContext db, UserManager<ApplicationUser> users) : IAccessService
{
    public async Task<AccessState> GetAccessAsync(int inventoryId, ApplicationUser? user)
    {
        var inventory = await db.Inventories.AsNoTracking().FirstOrDefaultAsync(i => i.Id == inventoryId);
        if (inventory is null) return new(false, false, false, false);

        var isAdmin = user is not null && await users.IsInRoleAsync(user, "Admin");
        var isOwner = user is not null && inventory.OwnerId == user.Id;
        var explicitAccess = user is not null && await db.InventoryAccesses
            .AnyAsync(a => a.InventoryId == inventoryId && a.UserId == user.Id);

        var canManage = isAdmin || isOwner;
        var canWrite = canManage || (user is not null && (inventory.IsPublic || explicitAccess));
        return new(true, canWrite, canManage, isAdmin);
    }

    public async Task<IReadOnlyList<ApplicationUser>> FindUsersAsync(string term)
    {
        term = term.Trim().ToUpperInvariant();
        if (term.Length < 2) return [];
        return await users.Users.AsNoTracking()
            .Where(u => !u.IsBlocked && (u.NormalizedUserName!.StartsWith(term) || u.NormalizedEmail!.StartsWith(term)))
            .OrderBy(u => u.UserName)
            .Take(10)
            .ToListAsync();
    }

    public async Task<ServiceResult> GrantAsync(int inventoryId, string userId, ApplicationUser actor)
    {
        var access = await GetAccessAsync(inventoryId, actor);
        if (!access.CanManage) return ServiceResult.Fail("Access denied.");
        if (!await users.Users.AnyAsync(u => u.Id == userId)) return ServiceResult.Fail("User not found.");
        if (!await db.InventoryAccesses.AnyAsync(a => a.InventoryId == inventoryId && a.UserId == userId))
        {
            db.InventoryAccesses.Add(new() { InventoryId = inventoryId, UserId = userId });
            await db.SaveChangesAsync();
        }
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> RevokeAsync(int inventoryId, string userId, ApplicationUser actor)
    {
        var access = await GetAccessAsync(inventoryId, actor);
        if (!access.CanManage) return ServiceResult.Fail("Access denied.");
        var row = await db.InventoryAccesses.FirstOrDefaultAsync(a => a.InventoryId == inventoryId && a.UserId == userId);
        if (row is not null)
        {
            db.InventoryAccesses.Remove(row);
            await db.SaveChangesAsync();
        }
        return ServiceResult.Ok();
    }
}
