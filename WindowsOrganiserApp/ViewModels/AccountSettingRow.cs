using CommunityToolkit.Mvvm.ComponentModel;
using SpecialAzerothService.Core.Models.Carto;

namespace WindowsOrganiserApp.ViewModels;

public partial class AccountSettingRow : ObservableObject
{
    public string SourceFolder { get; init; } = "";

    [ObservableProperty]
    private string _displayName = "";

    [ObservableProperty]
    private string? _userId;

    [ObservableProperty]
    private string _ownerDisplayName = "Non assigné";

    public int CharacterCount { get; init; }

    public long GoldCopper { get; init; }

    public CartoAccountConfig ToConfig(CartoAccountConfig? previous = null)
    {
        var name = DisplayName.Trim();
        return new CartoAccountConfig
        {
            DisplayName = string.IsNullOrWhiteSpace(name)
                ? previous?.DisplayName ?? SourceFolder
                : name,
            UserId = !string.IsNullOrWhiteSpace(UserId) ? UserId : previous?.UserId,
            Scope = previous?.Scope ?? AccountScope.Mine,
            FriendLabel = previous?.FriendLabel,
            IsHiddenOnMap = previous?.IsHiddenOnMap ?? false
        };
    }

    public static AccountSettingRow From(
        string sourceFolder,
        CartoAccountConfig? config,
        int characterCount,
        long goldCopper = 0,
        IEnumerable<CartoUser>? users = null)
    {
        config ??= new CartoAccountConfig { DisplayName = sourceFolder };
        var userId = config.UserId;
        return new AccountSettingRow
        {
            SourceFolder = sourceFolder,
            DisplayName = string.IsNullOrWhiteSpace(config.DisplayName) ? sourceFolder : config.DisplayName,
            UserId = userId,
            OwnerDisplayName = ResolveOwnerDisplayName(userId, users),
            CharacterCount = characterCount,
            GoldCopper = goldCopper
        };
    }

    public void RefreshOwnerDisplayName(IEnumerable<CartoUser> users) =>
        OwnerDisplayName = ResolveOwnerDisplayName(UserId, users);

    public static string ResolveOwnerDisplayName(string? userId, IEnumerable<CartoUser>? users)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return "Non assigné";

        if (users == null)
            return "—";

        return users.FirstOrDefault(u => u.Id == userId)?.Name ?? "Utilisateur inconnu";
    }
}
