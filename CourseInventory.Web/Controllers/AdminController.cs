using CourseInventory.Web.Models;
using CourseInventory.Web.Services;
using CourseInventory.Web.Data;
using CourseInventory.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CourseInventory.Web.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController(
    UserManager<ApplicationUser> users,
    ApplicationDbContext db,
    IUserActivityService userActivity) : Controller
{
    public async Task<IActionResult> Users()
    {
        return View(await BuildUsersViewModelAsync());
    }

    [HttpGet]
    public async Task<IActionResult> UsersSnapshot()
    {
        var model = await BuildUsersViewModelAsync();
        return Json(new
        {
            totalUsers = model.TotalUsers,
            onlineUsers = model.OnlineUsers,
            offlineUsers = model.OfflineUsers,
            blockedUsers = model.BlockedUsers,
            loginSessionsToday = model.LoginSessionsToday,
            busiestDayLabel = model.BusiestDayLabel,
            latestLoginUser = model.LatestLoginUser is null ? null : new
            {
                model.LatestLoginUser.UserName,
                model.LatestLoginUser.Email
            },
            loginActivity = model.LoginActivity,
            users = model.Users.Select(user => new
            {
                user.Id,
                user.IsOnline,
                lastSeen = user.LastSeenAt?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "Never",
                lastLogin = user.LastLoginAt?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "Never"
            })
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleBlock(string id)
    {
        var currentUser = await users.GetUserAsync(User);
        if (currentUser is not null && currentUser.Id == id)
        {
            TempData["Error"] = "You cannot block your own account.";
            return RedirectToAction(nameof(Users));
        }

        var user = await users.FindByIdAsync(id);
        if (user is not null)
        {
            user.IsBlocked = !user.IsBlocked;
            await users.UpdateAsync(user);
        }
        return RedirectToAction(nameof(Users));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleAdmin(string id)
    {
        var currentUser = await users.GetUserAsync(User);
        if (currentUser is not null && currentUser.Id == id)
        {
            TempData["Error"] = "You cannot change your own admin role.";
            return RedirectToAction(nameof(Users));
        }

        var user = await users.FindByIdAsync(id);
        if (user is not null)
        {
            if (await users.IsInRoleAsync(user, "Admin")) await users.RemoveFromRoleAsync(user, "Admin");
            else await users.AddToRoleAsync(user, "Admin");
        }
        return RedirectToAction(nameof(Users));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var currentUser = await users.GetUserAsync(User);
        if (currentUser is not null && currentUser.Id == id)
        {
            TempData["Error"] = "You cannot delete your own account.";
            return RedirectToAction(nameof(Users));
        }

        var user = await users.FindByIdAsync(id);
        if (user is null)
        {
            TempData["Error"] = "User not found.";
            return RedirectToAction(nameof(Users));
        }

        var ownedInventories = await db.Inventories.CountAsync(i => i.OwnerId == id);
        if (ownedInventories > 0)
        {
            TempData["Error"] = ownedInventories == 1
                ? "Cannot delete this user because they still own 1 inventory."
                : $"Cannot delete this user because they still own {ownedInventories} inventories.";
            return RedirectToAction(nameof(Users));
        }

        if (await db.InventoryItems.AnyAsync(i => i.CreatedById == id))
        {
            TempData["Error"] = "Cannot delete this user because they are referenced by existing items.";
            return RedirectToAction(nameof(Users));
        }

        if (await db.DiscussionMessages.AnyAsync(m => m.AuthorId == id))
        {
            TempData["Error"] = "Cannot delete this user because they are referenced by discussion messages.";
            return RedirectToAction(nameof(Users));
        }

        if (await db.InventoryAccesses.AnyAsync(a => a.UserId == id))
        {
            TempData["Error"] = "Cannot delete this user because they still have inventory access assignments.";
            return RedirectToAction(nameof(Users));
        }

        if (await db.ItemLikes.AnyAsync(l => l.UserId == id))
        {
            TempData["Error"] = "Cannot delete this user because they are referenced by likes.";
            return RedirectToAction(nameof(Users));
        }

        var result = await users.DeleteAsync(user);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded
            ? "User deleted."
            : result.Errors.FirstOrDefault()?.Description ?? "User could not be deleted.";
        return RedirectToAction(nameof(Users));
    }

    private async Task<AdminUsersViewModel> BuildUsersViewModelAsync()
    {
        var now = DateTime.UtcNow;
        var currentUser = await users.GetUserAsync(User);
        var usersList = await users.Users
            .AsNoTracking()
            .OrderBy(u => u.UserName)
            .ToListAsync();

        var adminUserIds = await (
                from userRole in db.UserRoles.AsNoTracking()
                join role in db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                where role.Name == "Admin"
                select userRole.UserId)
            .ToListAsync();
        var adminUserIdSet = adminUserIds.ToHashSet(StringComparer.Ordinal);

        var loginStartUtc = now.Date.AddDays(-6);
        var loginEntries = await db.UserLoginActivities.AsNoTracking()
            .Where(a => a.LoggedInAt >= loginStartUtc)
            .OrderBy(a => a.LoggedInAt)
            .Select(a => new
            {
                Date = a.LoggedInAt.Date,
                a.LoggedInAt,
                a.UserId,
                UserName = a.User != null ? a.User.UserName : null,
                Email = a.User != null ? a.User.Email : null
            })
            .ToListAsync();
        var loginEntryMap = loginEntries
            .GroupBy(entry => entry.Date)
            .ToDictionary(group => group.Key, group => group.ToList());

        var rows = usersList
            .Select(user => new AdminUserRowViewModel
            {
                Id = user.Id,
                UserName = user.UserName ?? "Unknown",
                Email = user.Email,
                IsBlocked = user.IsBlocked,
                IsAdmin = adminUserIdSet.Contains(user.Id),
                IsOnline = userActivity.IsOnline(user.LastSeenAt, now),
                IsCurrentUser = currentUser is not null && user.Id == currentUser.Id,
                CanBlock = currentUser is null || user.Id != currentUser.Id,
                CanDelete = currentUser is null || user.Id != currentUser.Id,
                CanToggleAdmin = currentUser is null || user.Id != currentUser.Id,
                DisabledActionReason = currentUser is not null && user.Id == currentUser.Id
                    ? "Self actions are disabled for security."
                    : null,
                CreatedAt = user.CreatedAt,
                LastSeenAt = user.LastSeenAt,
                LastLoginAt = user.LastLoginAt
            })
            .OrderByDescending(user => user.IsOnline)
            .ThenBy(user => user.UserName)
            .ToList();

        var activity = Enumerable.Range(0, 7)
            .Select(offset =>
            {
                var day = loginStartUtc.AddDays(offset);
                var entries = loginEntryMap.GetValueOrDefault(day) ?? [];
                return new AdminLoginActivityPointViewModel
                {
                    Label = day.ToLocalTime().ToString("dd/MM"),
                    FullLabel = day.ToLocalTime().ToString("dddd dd MMMM"),
                    Count = entries.Count,
                    Entries = entries
                        .OrderByDescending(entry => entry.LoggedInAt)
                        .Select(entry => new AdminLoginActivityEntryViewModel
                        {
                            UserId = entry.UserId,
                            UserName = entry.UserName ?? "Unknown",
                            Email = entry.Email,
                            TimeLabel = entry.LoggedInAt.ToLocalTime().ToString("HH:mm")
                        })
                        .ToList()
                };
            })
            .ToList();

        var latestLogin = rows
            .Where(user => user.LastLoginAt is not null)
            .OrderByDescending(user => user.LastLoginAt)
            .FirstOrDefault();
        var todayPoint = activity.LastOrDefault();
        var busiestDay = activity.OrderByDescending(point => point.Count).FirstOrDefault();

        return new AdminUsersViewModel
        {
            CurrentUserId = currentUser?.Id ?? string.Empty,
            TotalUsers = rows.Count,
            OnlineUsers = rows.Count(user => user.IsOnline),
            OfflineUsers = rows.Count(user => !user.IsOnline),
            BlockedUsers = rows.Count(user => user.IsBlocked),
            LoginSessionsToday = todayPoint?.Count ?? 0,
            BusiestDayLabel = busiestDay?.Label ?? "--",
            LatestLoginUser = latestLogin,
            LoginActivity = activity,
            Users = rows
        };
    }
}
