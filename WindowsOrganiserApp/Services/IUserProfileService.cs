using WindowsOrganiserApp.Models;
using WindowsOrganiserApp.Models.Carto;

namespace WindowsOrganiserApp.Services;

/// <summary>Profil utilisateur local (GUID, amis) — sans réseau.</summary>
public interface IUserProfileService
{
    string UserGuid { get; }
    IReadOnlyList<FriendEntry> Friends { get; }
    FriendEntry? GetFriend(string guid);
    void AddOrUpdateFriend(string guid, string name);
    void RemoveFriend(string guid);
    void Save();
}
