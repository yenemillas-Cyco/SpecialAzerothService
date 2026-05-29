using System.Collections.Concurrent;

namespace CartoSyncServer;

public class SubscriptionStore
{
    private readonly ConcurrentDictionary<string, HashSet<string>> _subscriptions = new();
    private readonly ConcurrentDictionary<string, string> _dataCache = new();
    private readonly ConcurrentDictionary<string, string> _bountyCache = new();
    private readonly ConcurrentDictionary<string, HashSet<string>> _connections = new();
    private readonly string _dataDir;
    private readonly string _bountyDir;
    private readonly ILogger<SubscriptionStore> _log;

    public SubscriptionStore(ILogger<SubscriptionStore> log)
    {
        _log = log;
        var baseDir = Environment.GetEnvironmentVariable("DATA_DIR") ?? "/data";
        _dataDir = Path.Combine(baseDir, "carto-cache");
        _bountyDir = Path.Combine(baseDir, "bounty-cache");
        Directory.CreateDirectory(_dataDir);
        Directory.CreateDirectory(_bountyDir);
        LoadAllFromDisk();
    }

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

    public string? GetCachedData(string userGuid)
        => _dataCache.TryGetValue(userGuid, out var d) ? d : null;

    public void CacheBountyData(string userGuid, string json)
    {
        _bountyCache[userGuid] = json;
        SaveToDisk(userGuid, json, _bountyDir);
    }

    public string? GetCachedBountyData(string userGuid)
        => _bountyCache.TryGetValue(userGuid, out var d) ? d : null;

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

    public void CacheData(string userGuid, string json)
    {
        _dataCache[userGuid] = json;
        SaveToDisk(userGuid, json, _dataDir);
    }

    private void SaveToDisk(string userGuid, string json, string dir)
    {
        try
        {
            var path = Path.Combine(dir, $"{userGuid}.json");
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to persist cache for {Guid}", userGuid[..8]);
        }
    }

    private void LoadAllFromDisk()
    {
        try
        {
            foreach (var file in Directory.GetFiles(_dataDir, "*.json"))
            {
                var guid = Path.GetFileNameWithoutExtension(file);
                _dataCache[guid] = File.ReadAllText(file);
            }
            foreach (var file in Directory.GetFiles(_bountyDir, "*.json"))
            {
                var guid = Path.GetFileNameWithoutExtension(file);
                _bountyCache[guid] = File.ReadAllText(file);
            }
            _log.LogInformation("Loaded {Carto} carto + {Bounty} bounty cached entries",
                _dataCache.Count, _bountyCache.Count);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to load cache from disk");
        }
    }
}
