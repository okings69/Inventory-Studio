using CourseInventory.Web.Data;
using CourseInventory.Web.Models;
using CourseInventory.Web.Models.Inventory;
using Microsoft.EntityFrameworkCore;

namespace CourseInventory.Web.Services;

public interface IDiscussionService
{
    Task<IReadOnlyList<DiscussionMessage>> LatestAsync(int inventoryId);
    Task<(ServiceResult Result, DiscussionMessage? Message)> AddAsync(int inventoryId, string markdown, ApplicationUser actor);
}

public class DiscussionService(ApplicationDbContext db, IAccessService access, IMarkdownService markdown) : IDiscussionService
{
    public async Task<IReadOnlyList<DiscussionMessage>> LatestAsync(int inventoryId) =>
        await db.DiscussionMessages.AsNoTracking()
            .Include(m => m.Author)
            .Where(m => m.InventoryId == inventoryId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(50)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();

    public async Task<(ServiceResult Result, DiscussionMessage? Message)> AddAsync(int inventoryId, string body, ApplicationUser actor)
    {
        var state = await access.GetAccessAsync(inventoryId, actor);
        if (!state.CanWrite) return (ServiceResult.Fail("Access denied."), null);
        if (string.IsNullOrWhiteSpace(body)) return (ServiceResult.Fail("Message is empty."), null);

        var message = new DiscussionMessage
        {
            InventoryId = inventoryId,
            AuthorId = actor.Id,
            BodyMarkdown = body.Trim(),
            BodyHtml = markdown.ToHtml(body.Trim())
        };
        db.DiscussionMessages.Add(message);
        await db.SaveChangesAsync();
        await db.Entry(message).Reference(m => m.Author).LoadAsync();
        return (ServiceResult.Ok(), message);
    }
}
