using System.Windows;

namespace WindowsOrganiserApp.Services;

public interface ILocalizationService
{
    string CurrentLanguage { get; }
    string[] AvailableLanguages { get; }
    void ApplyLanguage(string langCode);
}

public class LocalizationService : ILocalizationService
{
    private static readonly Dictionary<string, string> LangFiles = new()
    {
        ["Français"] = "Localization/Strings.fr.xaml",
        ["English"] = "Localization/Strings.en.xaml"
    };

    public string CurrentLanguage { get; private set; } = "Français";
    public string[] AvailableLanguages => [.. LangFiles.Keys];

    public void ApplyLanguage(string langName)
    {
        if (!LangFiles.TryGetValue(langName, out var file)) return;

        var app = Application.Current;
        var merged = app.Resources.MergedDictionaries;

        var existing = merged.FirstOrDefault(d =>
            d.Source?.OriginalString.Contains("Localization/") == true);
        if (existing is not null)
            merged.Remove(existing);

        merged.Add(new ResourceDictionary { Source = new Uri(file, UriKind.Relative) });
        CurrentLanguage = langName;
    }
}
