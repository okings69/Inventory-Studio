using CourseInventory.Web.Models;
using CourseInventory.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;

namespace CourseInventory.Web.Hubs;

public class InventoryDiscussionHub(
    IDiscussionService discussion,
    IAccessService access,
    IDiscussionPresenceService presence,
    IUserActivityService userActivity,
    UserManager<ApplicationUser> users) : Hub
{
    public async Task JoinInventoryGroup(int inventoryId)
    {
        var user = Context.User?.Identity?.IsAuthenticated == true
            ? await users.GetUserAsync(Context.User)
            : null;
        // The live discussion group uses the same rule as the HTTP chat endpoint:
        // the user must be authenticated and able to read the inventory.
        var accessState = await access.GetAccessAsync(inventoryId, user);
        if (!accessState.CanRead)
        {
            throw new HubException("Access denied.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(inventoryId));

        if (user is not null)
        {
            await userActivity.TouchUserAsync(user.Id);
            var change = presence.JoinInventory(inventoryId, user.Id, Context.ConnectionId);
            await Clients.Group(GroupName(inventoryId)).SendAsync("PresenceChanged", new
            {
                inventoryId = change.InventoryId,
                onlineUserIds = change.OnlineUserIds
            });
        }
    }

    public async Task LeaveInventoryGroup(int inventoryId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(inventoryId));
        var change = presence.LeaveInventory(inventoryId, Context.ConnectionId);
        await Clients.Group(GroupName(inventoryId)).SendAsync("PresenceChanged", new
        {
            inventoryId = change.InventoryId,
            onlineUserIds = change.OnlineUserIds
        });
    }

    [Authorize]
    public async Task SendMessage(int inventoryId, string markdown)
    {
        var user = await users.GetUserAsync(Context.User!);
        if (user is null) return;
        var accessState = await access.GetAccessAsync(inventoryId, user);
        if (!accessState.CanRead)
        {
            await Clients.Caller.SendAsync("MessageRejected", "Access denied.");
            return;
        }
        await userActivity.TouchUserAsync(user.Id);
        var (result, message) = await discussion.AddAsync(inventoryId, markdown, user);
        if (!result.Success || message is null)
        {
            await Clients.Caller.SendAsync("MessageRejected", result.Error);
            return;
        }

        await Clients.Group(GroupName(inventoryId)).SendAsync("ReceiveMessage", new
        {
            authorId = message.AuthorId,
            author = message.Author?.UserName ?? "user",
            createdAt = message.CreatedAt.ToString("u"),
            html = message.BodyHtml
        });
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var changes = presence.Disconnect(Context.ConnectionId);
        foreach (var change in changes)
        {
            await Clients.Group(GroupName(change.InventoryId)).SendAsync("PresenceChanged", new
            {
                inventoryId = change.InventoryId,
                onlineUserIds = change.OnlineUserIds
            });
        }

        await base.OnDisconnectedAsync(exception);
    }

    private static string GroupName(int inventoryId) => $"inventory-{inventoryId}";
}
