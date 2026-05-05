using System.Security.Claims;
using CourseInventory.Web.Data;
using CourseInventory.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CourseInventory.Web.Services;

public interface IUserActivityService
{
    Task TouchAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);
    Task TouchUserAsync(string userId, CancellationToken cancellationToken = default);
    Task RecordLoginAsync(ApplicationUser user, CancellationToken cancellationToken = default);
    bool IsOnline(DateTime? lastSeenAt, DateTime utcNow);
}

public class UserActivityService(ApplicationDbContext db, IMemoryCache cache) : IUserActivityService
{
    private static readonly TimeSpan OnlineThreshold = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan TouchThrottle = TimeSpan.FromMinutes(2);

    public bool IsOnline(DateTime? lastSeenAt, DateTime utcNow) =>
        lastSeenAt is not null && utcNow - lastSeenAt.Value <= OnlineThreshold;

    public async Task TouchAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        await TouchUserAsync(userId, cancellationToken);
    }

    public async Task TouchUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"user-last-seen:{userId}";
        if (cache.TryGetValue<DateTime>(cacheKey, out var lastTouched) &&
            DateTime.UtcNow - lastTouched < TouchThrottle)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var affected = await db.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(u => u.LastSeenAt, now), cancellationToken);

        if (affected > 0)
        {
            cache.Set(cacheKey, now, TouchThrottle);
        }
    }

    public async Task RecordLoginAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        user.LastSeenAt = now;
        user.LastLoginAt = now;

        db.UserLoginActivities.Add(new UserLoginActivity
        {
            UserId = user.Id,
            LoggedInAt = now
        });

        await db.SaveChangesAsync(cancellationToken);
        cache.Set($"user-last-seen:{user.Id}", now, TouchThrottle);
    }
}
