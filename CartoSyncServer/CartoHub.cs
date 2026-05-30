using System.Text.Json;
using Microsoft.AspNetCore.SignalR;

namespace CartoSyncServer;

public class CartoHub : Hub
{
    private readonly SubscriptionStore _store;
    private readonly FriendshipStore _friends;
    private readonly ILogger<CartoHub> _log;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public CartoHub(SubscriptionStore store, FriendshipStore friends, ILogger<CartoHub> log)
    {
        _store = store;
        _friends = friends;
        _log = log;
    }

    public async Task Connect(string userGuid)
    {
        userGuid = userGuid.Trim();
        await Groups.AddToGroupAsync(Context.ConnectionId, userGuid);
        await Groups.AddToGroupAsync(Context.ConnectionId, SubscriptionStore.TpBoyPublicGroup);
        _store.AddConnection(userGuid, Context.ConnectionId);
        _log.LogInformation("Connect: {Guid}", Short(userGuid));

        await Clients.OthersInGroup(userGuid).SendAsync("FriendOnline", userGuid);

        await SendTpBoyCatalogToCaller();
        await SendMutualFriendCachesToCaller(userGuid);
    }

    public async Task Subscribe(string myGuid, string friendGuid)
    {
        myGuid = myGuid.Trim();
        friendGuid = friendGuid.Trim();

        _friends.Register(myGuid, friendGuid);
        var state = _friends.GetLinkState(myGuid, friendGuid);
        await Clients.Caller.SendAsync("FriendLinkState", friendGuid, state.ToString());

        if (state == ServerFriendLinkState.Mutual)
        {
            await ActivateMutualSubscription(myGuid, friendGuid);
            await NotifyMutualIfNeeded(myGuid, friendGuid);
        }
        else
        {
            _log.LogInformation("Subscribe pending: {User} -> {Friend} ({State})",
                Short(myGuid), Short(friendGuid), state);
        }
    }

    public async Task Unsubscribe(string myGuid, string friendGuid)
    {
        myGuid = myGuid.Trim();
        friendGuid = friendGuid.Trim();
        _friends.Unregister(myGuid, friendGuid);
        _store.RemoveSubscription(myGuid, friendGuid);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, friendGuid);
        await Clients.Caller.SendAsync("FriendLinkState", friendGuid, ServerFriendLinkState.PendingOutbound.ToString());
        _log.LogInformation("Unsubscribe: {User} -> {Friend}", Short(myGuid), Short(friendGuid));
    }

    /// <summary>Envoie ami + TP Boy si la révision locale est plus récente que le cache serveur.</summary>
    public async Task PushFriendUpdate(string userGuid, long revision, string jsonData)
    {
        userGuid = userGuid.Trim();
        var existing = _store.GetFriendCache(userGuid);
        if (!_store.ShouldAcceptPush(revision, existing))
            return;

        var sentAt = TryReadSentAt(jsonData) ?? DateTimeOffset.UtcNow;
        _store.CacheFriendData(userGuid, jsonData, revision, sentAt);

        foreach (var subscriberGuid in _store.GetSubscribersOf(userGuid))
        {
            if (!_friends.IsMutual(userGuid, subscriberGuid))
                continue;

            await Clients.Group(subscriberGuid).SendAsync("ReceiveFriendUpdate", userGuid, jsonData);
        }

        _log.LogInformation("PushFriendUpdate: {Guid} rev={Rev} {Size}b",
            Short(userGuid), revision, jsonData.Length);
    }

    public async Task PushTpBoyPublic(string userGuid, long revision, string jsonData)
    {
        userGuid = userGuid.Trim();
        var existing = _store.GetTpBoyCache(userGuid);
        if (!_store.ShouldAcceptPush(revision, existing))
            return;

        var sentAt = TryReadSentAt(jsonData) ?? DateTimeOffset.UtcNow;
        _store.CacheTpBoyData(userGuid, jsonData, revision, sentAt);

        await Clients.OthersInGroup(SubscriptionStore.TpBoyPublicGroup)
            .SendAsync("ReceiveTpBoyPublic", userGuid, jsonData);

        _log.LogInformation("PushTpBoyPublic: {Guid} rev={Rev} {Size}b",
            Short(userGuid), revision, jsonData.Length);
    }

    /// <summary>Compatibilité anciens clients.</summary>
    public Task PushUpdate(string userGuid, string jsonData)
    {
        var revision = TryReadRevision(jsonData) ?? DateTimeOffset.UtcNow.Ticks;
        return PushFriendUpdate(userGuid, revision, jsonData);
    }

    public async Task PushBountyUpdate(string userGuid, string jsonData)
    {
        userGuid = userGuid.Trim();
        _store.CacheBountyData(userGuid, jsonData);

        await Clients.OthersInGroup(userGuid).SendAsync("ReceiveBountyUpdate", userGuid, jsonData);
        _log.LogInformation("PushBountyUpdate: {Guid}, {Size} bytes", Short(userGuid), jsonData.Length);
    }

    public async Task RequestFullRefresh(string userGuid)
    {
        userGuid = userGuid.Trim();
        await SendTpBoyCatalogToCaller();
        await SendMutualFriendCachesToCaller(userGuid);
        _log.LogInformation("RequestFullRefresh: {Guid}", Short(userGuid));
    }

    public List<string> GetOnlineFriends(List<string> friendGuids) =>
        _store.GetOnlineUsers(friendGuids);

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userGuid = _store.GetUserGuidByConnection(Context.ConnectionId);
        _store.RemoveConnection(Context.ConnectionId);

        if (userGuid != null)
        {
            await Clients.OthersInGroup(userGuid).SendAsync("FriendOffline", userGuid);
            _log.LogInformation("Disconnected: {User}", Short(userGuid));
        }

        await base.OnDisconnectedAsync(exception);
    }

    private async Task ActivateMutualSubscription(string myGuid, string friendGuid)
    {
        _store.AddSubscription(myGuid, friendGuid);
        await Groups.AddToGroupAsync(Context.ConnectionId, friendGuid);

        var data = _store.GetFriendCache(friendGuid);
        if (data != null)
            await Clients.Caller.SendAsync("ReceiveFriendUpdate", friendGuid, data.Json);

        var bountyData = _store.GetBountyCache(friendGuid);
        if (bountyData != null)
            await Clients.Caller.SendAsync("ReceiveBountyUpdate", friendGuid, bountyData.Json);

        if (data == null && _store.IsOnline(friendGuid))
            await Clients.Group(friendGuid).SendAsync("RequestPush");

        await Clients.Caller.SendAsync(
            _store.IsOnline(friendGuid) ? "FriendOnline" : "FriendOffline", friendGuid);
    }

    private async Task NotifyMutualIfNeeded(string myGuid, string friendGuid)
    {
        await Clients.Group(friendGuid).SendAsync("FriendLinkState", myGuid, ServerFriendLinkState.Mutual.ToString());

        var myCache = _store.GetFriendCache(myGuid);
        if (myCache != null)
            await Clients.Group(friendGuid).SendAsync("ReceiveFriendUpdate", myGuid, myCache.Json);
    }

    private async Task SendMutualFriendCachesToCaller(string userGuid)
    {
        var friendGuids = _store.GetSubscriptions(userGuid)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var friendGuid in friendGuids)
        {
            var state = _friends.GetLinkState(userGuid, friendGuid);
            await Clients.Caller.SendAsync("FriendLinkState", friendGuid, state.ToString());

            if (!_friends.IsMutual(userGuid, friendGuid))
                continue;

            var data = _store.GetFriendCache(friendGuid);
            if (data != null)
                await Clients.Caller.SendAsync("ReceiveFriendUpdate", friendGuid, data.Json);

            var bountyData = _store.GetBountyCache(friendGuid);
            if (bountyData != null)
                await Clients.Caller.SendAsync("ReceiveBountyUpdate", friendGuid, bountyData.Json);
        }
    }

    private async Task SendTpBoyCatalogToCaller()
    {
        foreach (var (ownerGuid, blob) in _store.GetAllTpBoyCaches())
            await Clients.Caller.SendAsync("ReceiveTpBoyPublic", ownerGuid, blob.Json);
    }

    private static DateTimeOffset? TryReadSentAt(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("sentAt", out var el) && el.TryGetDateTimeOffset(out var dto))
                return dto;
        }
        catch { }
        return null;
    }

    private static long? TryReadRevision(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("revision", out var el) && el.TryGetInt64(out var rev))
                return rev;
        }
        catch { }
        return null;
    }

    private static string Short(string guid) =>
        guid.Length > 8 ? guid[..8] : guid;
}
