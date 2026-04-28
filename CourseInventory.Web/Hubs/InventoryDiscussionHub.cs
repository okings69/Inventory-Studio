using CourseInventory.Web.Models;
using CourseInventory.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;

namespace CourseInventory.Web.Hubs;

public class InventoryDiscussionHub(IDiscussionService discussion, UserManager<ApplicationUser> users) : Hub
{
    public Task JoinInventoryGroup(int inventoryId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, GroupName(inventoryId));

    public Task LeaveInventoryGroup(int inventoryId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(inventoryId));

    [Authorize]
    public async Task SendMessage(int inventoryId, string markdown)
    {
        var user = await users.GetUserAsync(Context.User!);
        if (user is null) return;
        var (result, message) = await discussion.AddAsync(inventoryId, markdown, user);
        if (!result.Success || message is null)
        {
            await Clients.Caller.SendAsync("MessageRejected", result.Error);
            return;
        }

        await Clients.Group(GroupName(inventoryId)).SendAsync("ReceiveMessage", new
        {
            author = message.Author?.UserName ?? "user",
            createdAt = message.CreatedAt.ToString("u"),
            html = message.BodyHtml
        });
    }

    private static string GroupName(int inventoryId) => $"inventory-{inventoryId}";
}
