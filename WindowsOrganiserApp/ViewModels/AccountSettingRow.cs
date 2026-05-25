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

    public CartoAccountConfig ToConfig()
    {
        var name = DisplayName.Trim();
        return new CartoAccountConfig
        {
            DisplayName = name,
            UserId = UserId
        };
    }

    public static AccountSettingRow From(
        string sourceFolder,
        CartoAccountConfig? config,
        int characterCount)
    {
        config ??= new CartoAccountConfig { DisplayName = sourceFolder };
        return new AccountSettingRow
        {
            SourceFolder = sourceFolder,
            DisplayName = string.IsNullOrWhiteSpace(config.DisplayName) ? sourceFolder : config.DisplayName,
            UserId = config.UserId,
            CharacterCount = characterCount
        };
    }
}
