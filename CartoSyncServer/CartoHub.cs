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

    public async Task Connect(string userGuid, string userName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, userGuid);
        _store.SetUserName(userGuid, userName);
        _store.AddConnection(userGuid, Context.ConnectionId);
        _log.LogInformation("Connect: {User} ({Guid})", userName, userGuid[..8]);

        // Notify subscribers that this user is now online
        await Clients.OthersInGroup(userGuid).SendAsync("FriendOnline", userGuid);

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
        else if (_store.IsOnline(friendGuid))
        {
            // Friend is online but no cached data — ask them to push
            await Clients.Group(friendGuid).SendAsync("RequestPush");
            _log.LogInformation("RequestPush sent to {Friend}", friendGuid[..8]);
        }

        // Send current online status of the friend
        await Clients.Caller.SendAsync(
            _store.IsOnline(friendGuid) ? "FriendOnline" : "FriendOffline", friendGuid);
    }

    public async Task Unsubscribe(string myGuid, string friendGuid)
    {
        _store.RemoveSubscription(myGuid, friendGuid);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, friendGuid);
        _log.LogInformation("Unsubscribe: {User} -> {Friend}", myGuid[..8], friendGuid[..8]);
    }

    public async Task PushUpdate(string userGuid, string userName, string jsonData)
    {
        _store.CacheData(userGuid, jsonData);
        _store.SetUserName(userGuid, userName);

        await Clients.OthersInGroup(userGuid).SendAsync("ReceiveUpdate", userGuid, userName, jsonData);
        _log.LogInformation("PushUpdate: {User} ({Guid}), {Size} bytes",
            userName, userGuid[..8], jsonData.Length);
    }

    public List<string> GetOnlineFriends(List<string> friendGuids)
    {
        return _store.GetOnlineUsers(friendGuids);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userGuid = _store.GetUserGuidByConnection(Context.ConnectionId);
        _store.RemoveConnection(Context.ConnectionId);

        if (userGuid != null)
        {
            // Notify subscribers that this user went offline
            await Clients.OthersInGroup(userGuid).SendAsync("FriendOffline", userGuid);
            _log.LogInformation("Disconnected: {User}", userGuid[..8]);
        }

        await base.OnDisconnectedAsync(exception);
    }
}
