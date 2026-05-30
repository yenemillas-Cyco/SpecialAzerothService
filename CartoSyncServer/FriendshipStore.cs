using System.Collections.Concurrent;

namespace CartoSyncServer;

public enum ServerFriendLinkState
{
    PendingOutbound,
    PendingInbound,
    Mutual
}

public sealed class FriendshipStore
{
    private readonly ConcurrentDictionary<string, HashSet<string>> _outgoing = new(StringComparer.OrdinalIgnoreCase);

    public void Register(string userGuid, string friendGuid)
    {
        if (string.Equals(userGuid, friendGuid, StringComparison.OrdinalIgnoreCase))
            return;

        var set = _outgoing.GetOrAdd(Normalize(userGuid), _ => []);
        lock (set) { set.Add(Normalize(friendGuid)); }
    }

    public void Unregister(string userGuid, string friendGuid)
    {
        if (_outgoing.TryGetValue(Normalize(userGuid), out var set))
            lock (set) { set.Remove(Normalize(friendGuid)); }
    }

    public bool HasLink(string from, string to)
    {
        if (!_outgoing.TryGetValue(Normalize(from), out var set))
            return false;
        lock (set) { return set.Contains(Normalize(to)); }
    }

    public bool IsMutual(string a, string b) => HasLink(a, b) && HasLink(b, a);

    public ServerFriendLinkState GetLinkState(string me, string other)
    {
        var iHave = HasLink(me, other);
        var theyHave = HasLink(other, me);
        if (iHave && theyHave) return ServerFriendLinkState.Mutual;
        if (iHave) return ServerFriendLinkState.PendingOutbound;
        return ServerFriendLinkState.PendingInbound;
    }

    private static string Normalize(string guid) => guid.Trim();
}
