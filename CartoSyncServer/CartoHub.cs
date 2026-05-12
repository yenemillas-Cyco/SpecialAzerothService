using Microsoft.AspNetCore.SignalR;

namespace CartoSyncServer;

public class CartoHub : Hub
{
    private readonly SubscriptionStore _store;
    private readonly ILogger<CartoHub> _log;

    public CartoHub(SubscriptionStore store, ILogger<CartoHub> log)
    {
        _store = store;
        _log = log;
    }

    /// <summary>
    /// Called when a client connects. Joins the user's own group
    /// so others can push updates to them.
    /// </summary>
    public async Task Connect(string userGuid, string userName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, userGuid);
        _store.SetUserName(userGuid, userName);
        _log.LogInformation("Connect: {User} ({Guid})", userName, userGuid[..8]);

        // Send cached data for each friend this user is subscribed to
        foreach (var friendGuid in _store.GetSubscriptions(userGuid))
        {
            var data = _store.GetCachedData(friendGuid);
            if (data != null)
            {
                var friendName = _store.GetUserName(friendGuid);
                await Clients.Caller.SendAsync("ReceiveUpdate", friendGuid, friendName, data);
            }
        }
    }

    /// <summary>
    /// Subscribe to a friend's updates by their Guid.
    /// Immediately receives their cached data if available.
    /// </summary>
    public async Task Subscribe(string myGuid, string friendGuid)
    {
        _store.AddSubscription(myGuid, friendGuid);
        await Groups.AddToGroupAsync(Context.ConnectionId, friendGuid);

        _log.LogInformation("Subscribe: {User} -> {Friend}", myGuid[..8], friendGuid[..8]);

        var data = _store.GetCachedData(friendGuid);
        if (data != null)
        {
            var friendName = _store.GetUserName(friendGuid);
            await Clients.Caller.SendAsync("ReceiveUpdate", friendGuid, friendName, data);
        }
    }

    /// <summary>
    /// Unsubscribe from a friend's updates.
    /// </summary>
    public async Task Unsubscribe(string myGuid, string friendGuid)
    {
        _store.RemoveSubscription(myGuid, friendGuid);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, friendGuid);
        _log.LogInformation("Unsubscribe: {User} -> {Friend}", myGuid[..8], friendGuid[..8]);
    }

    /// <summary>
    /// Push updated data. Broadcasts to all users subscribed to this Guid.
    /// </summary>
    public async Task PushUpdate(string userGuid, string userName, string jsonData)
    {
        _store.CacheData(userGuid, jsonData);
        _store.SetUserName(userGuid, userName);

        await Clients.OthersInGroup(userGuid).SendAsync("ReceiveUpdate", userGuid, userName, jsonData);
        _log.LogInformation("PushUpdate: {User} ({Guid}), {Size} bytes",
            userName, userGuid[..8], jsonData.Length);
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _log.LogInformation("Disconnected: {ConnId}", Context.ConnectionId[..8]);
        return base.OnDisconnectedAsync(exception);
    }
}
