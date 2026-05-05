using System.Collections.Concurrent;

namespace CourseInventory.Web.Services;

public interface IDiscussionPresenceService
{
    IReadOnlyCollection<string> GetOnlineUserIds(int inventoryId);
    IReadOnlyCollection<string> GetOnlineUserIds();
    PresenceChange JoinInventory(int inventoryId, string userId, string connectionId);
    PresenceChange LeaveInventory(int inventoryId, string connectionId);
    IReadOnlyList<PresenceChange> Disconnect(string connectionId);
}

public record PresenceChange(int InventoryId, IReadOnlyCollection<string> OnlineUserIds);

public class DiscussionPresenceService : IDiscussionPresenceService
{
    private readonly object gate = new();
    private readonly Dictionary<int, Dictionary<string, HashSet<string>>> inventoryUsers = [];
    private readonly Dictionary<string, ConnectionPresence> connections = [];

    public IReadOnlyCollection<string> GetOnlineUserIds(int inventoryId)
    {
        lock (gate)
        {
            return SnapshotOnlineUserIds(inventoryId);
        }
    }

    public IReadOnlyCollection<string> GetOnlineUserIds()
    {
        lock (gate)
        {
            return inventoryUsers.Values
                .SelectMany(users => users.Keys)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }
    }

    public PresenceChange JoinInventory(int inventoryId, string userId, string connectionId)
    {
        lock (gate)
        {
            if (!inventoryUsers.TryGetValue(inventoryId, out var users))
            {
                users = [];
                inventoryUsers[inventoryId] = users;
            }

            if (!users.TryGetValue(userId, out var connectionsForUser))
            {
                connectionsForUser = [];
                users[userId] = connectionsForUser;
            }

            connectionsForUser.Add(connectionId);

            if (!connections.TryGetValue(connectionId, out var connection))
            {
                connection = new ConnectionPresence(userId);
                connections[connectionId] = connection;
            }

            connection.InventoryIds.Add(inventoryId);

            return new PresenceChange(inventoryId, SnapshotOnlineUserIds(inventoryId));
        }
    }

    public PresenceChange LeaveInventory(int inventoryId, string connectionId)
    {
        lock (gate)
        {
            RemoveConnectionFromInventory(inventoryId, connectionId);
            return new PresenceChange(inventoryId, SnapshotOnlineUserIds(inventoryId));
        }
    }

    public IReadOnlyList<PresenceChange> Disconnect(string connectionId)
    {
        lock (gate)
        {
            if (!connections.TryGetValue(connectionId, out var connection))
            {
                return [];
            }

            var changes = new List<PresenceChange>();
            foreach (var inventoryId in connection.InventoryIds.ToArray())
            {
                RemoveConnectionFromInventory(inventoryId, connectionId);
                changes.Add(new PresenceChange(inventoryId, SnapshotOnlineUserIds(inventoryId)));
            }

            connections.Remove(connectionId);
            return changes;
        }
    }

    private void RemoveConnectionFromInventory(int inventoryId, string connectionId)
    {
        if (!connections.TryGetValue(connectionId, out var connection))
        {
            return;
        }

        if (!inventoryUsers.TryGetValue(inventoryId, out var users))
        {
            connection.InventoryIds.Remove(inventoryId);
            if (connection.InventoryIds.Count == 0)
            {
                connections.Remove(connectionId);
            }
            return;
        }

        if (users.TryGetValue(connection.UserId, out var connectionsForUser))
        {
            connectionsForUser.Remove(connectionId);
            if (connectionsForUser.Count == 0)
            {
                users.Remove(connection.UserId);
            }
        }

        if (users.Count == 0)
        {
            inventoryUsers.Remove(inventoryId);
        }

        connection.InventoryIds.Remove(inventoryId);
        if (connection.InventoryIds.Count == 0)
        {
            connections.Remove(connectionId);
        }
    }

    private IReadOnlyCollection<string> SnapshotOnlineUserIds(int inventoryId) =>
        inventoryUsers.TryGetValue(inventoryId, out var users)
            ? users.Keys.ToArray()
            : [];

    private sealed class ConnectionPresence(string userId)
    {
        public string UserId { get; } = userId;
        public HashSet<int> InventoryIds { get; } = [];
    }
}
