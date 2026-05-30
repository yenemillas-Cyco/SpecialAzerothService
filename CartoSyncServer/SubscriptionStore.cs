using System.Collections.Concurrent;
using System.Text.Json;

namespace CartoSyncServer;

public sealed class CachedSyncBlob
{
    public string Json { get; set; } = "";
    public long Revision { get; set; }
    public DateTimeOffset SentAt { get; set; }
}

public class SubscriptionStore
{
    public const string TpBoyPublicGroup = "tp-public";

    private readonly ConcurrentDictionary<string, HashSet<string>> _subscriptions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, CachedSyncBlob> _friendCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, CachedSyncBlob> _tpBoyCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, CachedSyncBlob> _bountyCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, HashSet<string>> _connections = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _friendDir;
    private readonly string _tpBoyDir;
    private readonly string _bountyDir;
    private readonly ILogger<SubscriptionStore> _log;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public SubscriptionStore(ILogger<SubscriptionStore> log)
    {
        _log = log;
        var baseDir = Environment.GetEnvironmentVariable("DATA_DIR") ?? "/data";
        _friendDir = Path.Combine(baseDir, "friend-cache");
        _tpBoyDir = Path.Combine(baseDir, "tpboy-cache");
        _bountyDir = Path.Combine(baseDir, "bounty-cache");
        Directory.CreateDirectory(_friendDir);
        Directory.CreateDirectory(_tpBoyDir);
        Directory.CreateDirectory(_bountyDir);
        LoadAllFromDisk();
    }

    public void AddSubscription(string userGuid, string friendGuid)
    {
        var subs = _subscriptions.GetOrAdd(Normalize(userGuid), _ => []);
        lock (subs) { subs.Add(Normalize(friendGuid)); }
    }

    public void RemoveSubscription(string userGuid, string friendGuid)
    {
        if (_subscriptions.TryGetValue(Normalize(userGuid), out var subs))
            lock (subs) { subs.Remove(Normalize(friendGuid)); }
    }

    public IReadOnlyList<string> GetSubscriptions(string userGuid)
    {
        if (_subscriptions.TryGetValue(Normalize(userGuid), out var subs))
            lock (subs) { return [.. subs]; }
        return [];
    }

    /// <summary>Utilisateurs qui se sont abonnés aux mises à jour de <paramref name="targetGuid"/>.</summary>
    public IReadOnlyList<string> GetSubscribersOf(string targetGuid)
    {
        var key = Normalize(targetGuid);
        var result = new List<string>();
        foreach (var (userGuid, subs) in _subscriptions)
        {
            lock (subs)
            {
                if (subs.Contains(key))
                    result.Add(userGuid);
            }
        }

        return result;
    }

    public CachedSyncBlob? GetFriendCache(string userGuid) =>
        _friendCache.TryGetValue(Normalize(userGuid), out var d) ? d : null;

    public IReadOnlyList<KeyValuePair<string, CachedSyncBlob>> GetAllTpBoyCaches()
    {
        return _tpBoyCache.Select(kv => kv).ToList();
    }

    public CachedSyncBlob? GetTpBoyCache(string userGuid) =>
        _tpBoyCache.TryGetValue(Normalize(userGuid), out var d) ? d : null;

    public CachedSyncBlob? GetBountyCache(string userGuid) =>
        _bountyCache.TryGetValue(Normalize(userGuid), out var d) ? d : null;

    public void CacheFriendData(string userGuid, string json, long revision, DateTimeOffset sentAt)
    {
        var blob = new CachedSyncBlob { Json = json, Revision = revision, SentAt = sentAt };
        _friendCache[Normalize(userGuid)] = blob;
        SaveEnvelope(userGuid, blob, _friendDir);
    }

    public void CacheTpBoyData(string userGuid, string json, long revision, DateTimeOffset sentAt)
    {
        var blob = new CachedSyncBlob { Json = json, Revision = revision, SentAt = sentAt };
        _tpBoyCache[Normalize(userGuid)] = blob;
        SaveEnvelope(userGuid, blob, _tpBoyDir);
    }

    public void CacheBountyData(string userGuid, string json)
    {
        var blob = new CachedSyncBlob { Json = json, Revision = 0, SentAt = DateTimeOffset.UtcNow };
        _bountyCache[Normalize(userGuid)] = blob;
        SaveEnvelope(userGuid, blob, _bountyDir);
    }

    public bool ShouldAcceptPush(long incomingRevision, CachedSyncBlob? existing) =>
        existing == null || incomingRevision != existing.Revision;

    public void AddConnection(string userGuid, string connectionId)
    {
        var conns = _connections.GetOrAdd(Normalize(userGuid), _ => []);
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
        if (_connections.TryGetValue(Normalize(userGuid), out var conns))
            lock (conns) { return conns.Count > 0; }
        return false;
    }

    public List<string> GetOnlineUsers(IEnumerable<string> guids) =>
        guids.Where(IsOnline).ToList();

    public int ConnectedUsers => _connections.Count(kv =>
    {
        lock (kv.Value) { return kv.Value.Count > 0; }
    });

    private void SaveEnvelope(string userGuid, CachedSyncBlob blob, string dir)
    {
        try
        {
            var envelope = new DiskEnvelope
            {
                Revision = blob.Revision,
                SentAt = blob.SentAt,
                Json = blob.Json
            };
            var path = Path.Combine(dir, $"{Normalize(userGuid)}.json");
            File.WriteAllText(path, JsonSerializer.Serialize(envelope, JsonOpts));
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to persist cache for {Guid}", userGuid[..Math.Min(8, userGuid.Length)]);
        }
    }

    private void LoadAllFromDisk()
    {
        try
        {
            LoadDir(_friendDir, _friendCache);
            LoadDir(_tpBoyDir, _tpBoyCache);
            LoadDir(_bountyDir, _bountyCache);
            _log.LogInformation("Loaded {Friend} friend, {Tp} tp, {Bounty} bounty cached entries",
                _friendCache.Count, _tpBoyCache.Count, _bountyCache.Count);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to load cache from disk");
        }
    }

    private void LoadDir(string dir, ConcurrentDictionary<string, CachedSyncBlob> target)
    {
        foreach (var file in Directory.GetFiles(dir, "*.json"))
        {
            try
            {
                var text = File.ReadAllText(file);
                var envelope = JsonSerializer.Deserialize<DiskEnvelope>(text, JsonOpts);
                if (envelope == null) continue;

                var guid = Path.GetFileNameWithoutExtension(file);
                target[guid] = new CachedSyncBlob
                {
                    Json = envelope.Json,
                    Revision = envelope.Revision,
                    SentAt = envelope.SentAt
                };
            }
            catch
            {
                // Ancien format brut : fichier = JSON payload seul
                var guid = Path.GetFileNameWithoutExtension(file);
                target[guid] = new CachedSyncBlob { Json = File.ReadAllText(file), Revision = 0, SentAt = DateTimeOffset.MinValue };
            }
        }
    }

    private static string Normalize(string guid) => guid.Trim();

    private sealed class DiskEnvelope
    {
        public long Revision { get; set; }
        public DateTimeOffset SentAt { get; set; }
        public string Json { get; set; } = "";
    }
}
