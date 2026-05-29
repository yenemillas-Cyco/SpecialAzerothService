using SpecialAzerothService.Core.Models;
using SpecialAzerothService.Core.Models.Carto;

namespace SpecialAzerothService.Core.Services;

public sealed class UserProfileService : IUserProfileService
{
    private readonly ISettingsService _settingsService;
    private readonly AppSettings _settings;

    public UserProfileService(AppSettings settings, ISettingsService settingsService)
    {
        _settings = settings;
        _settingsService = settingsService;
        if (string.IsNullOrWhiteSpace(_settings.UserGuid))
            _settings.UserGuid = Guid.NewGuid().ToString();
    }

    public string UserGuid => _settings.UserGuid;

    public IReadOnlyList<FriendEntry> Friends => _settings.Friends;

    public FriendEntry? GetFriend(string guid) =>
        _settings.Friends.FirstOrDefault(f => f.Guid == guid);

    public void AddOrUpdateFriend(string guid, string name)
    {
        var existing = _settings.Friends.FirstOrDefault(f => f.Guid == guid);
        if (existing != null)
        {
            existing.Name = name;
            return;
        }

        _settings.Friends.Add(new FriendEntry { Guid = guid, Name = name });
    }

    public void RemoveFriend(string guid) =>
        _settings.Friends.RemoveAll(f => f.Guid == guid);

    public void Save() => _settingsService.Save(_settings);
}
