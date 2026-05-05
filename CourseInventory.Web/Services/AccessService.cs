using CourseInventory.Web.Data;
using CourseInventory.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CourseInventory.Web.Services;

public interface IAccessService
{
    Task<AccessScope> BuildScopeAsync(ApplicationUser? user);
    Task<AccessState> GetAccessAsync(int inventoryId, ApplicationUser? user);
    IQueryable<Models.Inventory.Inventory> FilterReadableInventories(IQueryable<Models.Inventory.Inventory> query, AccessScope scope);
    Task<IReadOnlyList<ApplicationUser>> FindUsersAsync(string term);
    Task<ServiceResult> GrantAsync(int inventoryId, string userId, ApplicationUser actor);
    Task<ServiceResult> RevokeAsync(int inventoryId, string userId, ApplicationUser actor);
}

public class AccessService(ApplicationDbContext db, UserManager<ApplicationUser> users) : IAccessService
{
    public async Task<AccessScope> BuildScopeAsync(ApplicationUser? user)
    {
        if (user is null)
        {
            return new(false, false, null);
        }

        return new(true, await users.IsInRoleAsync(user, "Admin"), user.Id);
    }

    public async Task<AccessState> GetAccessAsync(int inventoryId, ApplicationUser? user)
    {
        var inventory = await db.Inventories.AsNoTracking()
            .Select(i => new { i.Id, i.OwnerId, i.IsPublic })
            .FirstOrDefaultAsync(i => i.Id == inventoryId);
        if (inventory is null)
        {
            return new(false, false, false, false);
        }

        var scope = await BuildScopeAsync(user);
        var isOwner = scope.UserId is not null && inventory.OwnerId == scope.UserId;
        var explicitAccess = false;

        if (scope.UserId is not null && !scope.IsAdmin && !isOwner)
        {
            explicitAccess = await db.InventoryAccesses.AsNoTracking()
                .AnyAsync(a => a.InventoryId == inventoryId && a.UserId == scope.UserId);
        }

        var canManage = scope.IsAdmin || isOwner;
        var canRead = inventory.IsPublic || canManage || explicitAccess;
        var canWrite = canManage || explicitAccess;

        return new(canRead, canWrite, canManage, scope.IsAdmin);
    }

    public IQueryable<Models.Inventory.Inventory> FilterReadableInventories(IQueryable<Models.Inventory.Inventory> query, AccessScope scope)
    {
        if (scope.IsAdmin)
        {
            return query;
        }

        if (!scope.IsAuthenticated || string.IsNullOrWhiteSpace(scope.UserId))
        {
            return query.Where(i => i.IsPublic);
        }

        var userId = scope.UserId;
        return query.Where(i => i.IsPublic || i.OwnerId == userId || i.Accesses.Any(a => a.UserId == userId));
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
