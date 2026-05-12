using System.Collections.Concurrent;

namespace CartoSyncServer;

public class SubscriptionStore
{
    private readonly ConcurrentDictionary<string, HashSet<string>> _subscriptions = new();
    private readonly ConcurrentDictionary<string, string> _dataCache = new();
    private readonly ConcurrentDictionary<string, string> _userNames = new();

    public void AddSubscription(string userGuid, string friendGuid)
    {
        var subs = _subscriptions.GetOrAdd(userGuid, _ => []);
        lock (subs) { subs.Add(friendGuid); }
    }

    public void RemoveSubscription(string userGuid, string friendGuid)
    {
        if (_subscriptions.TryGetValue(userGuid, out var subs))
            lock (subs) { subs.Remove(friendGuid); }
    }

    public IReadOnlyList<string> GetSubscriptions(string userGuid)
    {
        if (_subscriptions.TryGetValue(userGuid, out var subs))
            lock (subs) { return [.. subs]; }
        return [];
    }

    public void CacheData(string userGuid, string json) => _dataCache[userGuid] = json;

    public string? GetCachedData(string userGuid)
        => _dataCache.TryGetValue(userGuid, out var d) ? d : null;

    public void SetUserName(string userGuid, string name) => _userNames[userGuid] = name;

    public string GetUserName(string userGuid)
        => _userNames.TryGetValue(userGuid, out var n) ? n : "Inconnu";

    public int ConnectedUsers => _userNames.Count;
}
