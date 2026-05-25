using CommunityToolkit.Mvvm.ComponentModel;
using WindowsOrganiserApp.Models.Carto;

namespace WindowsOrganiserApp.ViewModels;

public partial class AccountSettingRow : ObservableObject
{
    public string SourceFolder { get; init; } = "";

    [ObservableProperty]
    private string _displayName = "";

    [ObservableProperty]
    private string? _userId;

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
        long goldCopper = 0)
    {
        config ??= new CartoAccountConfig { DisplayName = sourceFolder };
        return new AccountSettingRow
        {
            SourceFolder = sourceFolder,
            DisplayName = string.IsNullOrWhiteSpace(config.DisplayName) ? sourceFolder : config.DisplayName,
            UserId = config.UserId,
            CharacterCount = characterCount,
            GoldCopper = goldCopper
        };
    }
}
