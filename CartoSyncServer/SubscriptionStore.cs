using System.Collections.Concurrent;

namespace CartoSyncServer;

public class SubscriptionStore
{
    private readonly ConcurrentDictionary<string, HashSet<string>> _subscriptions = new();
    private readonly ConcurrentDictionary<string, string> _dataCache = new();
    private readonly ConcurrentDictionary<string, string> _userNames = new();
    private readonly ConcurrentDictionary<string, HashSet<string>> _connections = new();

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

    public void AddConnection(string userGuid, string connectionId)
    {
        var conns = _connections.GetOrAdd(userGuid, _ => []);
        lock (conns) { conns.Add(connectionId); }
    }

    public void RemoveConnection(string connectionId)
    {
        foreach (var (_, conns) in _connections)
            lock (conns) { conns.Remove(connectionId); }
    }

    public string? GetUserGuidByConnection(string connectionId)
    {
        foreach (var (guid, conns) in _connections)
            lock (conns) { if (conns.Contains(connectionId)) return guid; }
        return null;
    }

    public bool IsOnline(string userGuid)
    {
        if (_connections.TryGetValue(userGuid, out var conns))
            lock (conns) { return conns.Count > 0; }
        return false;
    }

    public List<string> GetOnlineUsers(IEnumerable<string> guids)
        => guids.Where(IsOnline).ToList();

    public int ConnectedUsers => _connections.Count(kv =>
    {
        lock (kv.Value) { return kv.Value.Count > 0; }
    });
}
