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

    public async Task Connect(string userGuid)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, userGuid);
        _store.AddConnection(userGuid, Context.ConnectionId);
        _log.LogInformation("Connect: {Guid}", userGuid[..8]);

        await Clients.OthersInGroup(userGuid).SendAsync("FriendOnline", userGuid);

        foreach (var friendGuid in _store.GetSubscriptions(userGuid))
        {
            var data = _store.GetCachedData(friendGuid);
            if (data != null)
                await Clients.Caller.SendAsync("ReceiveUpdate", friendGuid, data);

            var bountyData = _store.GetCachedBountyData(friendGuid);
            if (bountyData != null)
                await Clients.Caller.SendAsync("ReceiveBountyUpdate", friendGuid, bountyData);
        }
    }

    public async Task Subscribe(string myGuid, string friendGuid)
    {
        _store.AddSubscription(myGuid, friendGuid);
        await Groups.AddToGroupAsync(Context.ConnectionId, friendGuid);

        _log.LogInformation("Subscribe: {User} -> {Friend}", myGuid[..8], friendGuid[..8]);

        var data = _store.GetCachedData(friendGuid);
        if (data != null)
            await Clients.Caller.SendAsync("ReceiveUpdate", friendGuid, data);

        var bountyData = _store.GetCachedBountyData(friendGuid);
        if (bountyData != null)
            await Clients.Caller.SendAsync("ReceiveBountyUpdate", friendGuid, bountyData);

        if (data == null && _store.IsOnline(friendGuid))
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

    public async Task PushUpdate(string userGuid, string jsonData)
    {
        _store.CacheData(userGuid, jsonData);

        await Clients.OthersInGroup(userGuid).SendAsync("ReceiveUpdate", userGuid, jsonData);
        _log.LogInformation("PushUpdate: {Guid}, {Size} bytes", userGuid[..8], jsonData.Length);
    }

    public async Task PushBountyUpdate(string userGuid, string jsonData)
    {
        _store.CacheBountyData(userGuid, jsonData);

        await Clients.OthersInGroup(userGuid).SendAsync("ReceiveBountyUpdate", userGuid, jsonData);
        _log.LogInformation("PushBountyUpdate: {Guid}, {Size} bytes", userGuid[..8], jsonData.Length);
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
