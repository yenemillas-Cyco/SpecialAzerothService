using System.Windows;

namespace WindowsOrganiserApp.Services;

public interface IThemeService
{
    string CurrentTheme { get; }
    string[] AvailableThemes { get; }
    void ApplyTheme(string themeName);
}

public class ThemeService : IThemeService
{
    private static readonly Dictionary<string, string> ThemeFiles = new()
    {
        ["Classic"] = "Themes/ClassicTheme.xaml",
        ["Retail"] = "Themes/RetailTheme.xaml",
        ["Dark"] = "Themes/DarkTheme.xaml",
        ["Light"] = "Themes/LightTheme.xaml"
    };

    public string CurrentTheme { get; private set; } = "Classic";
    public string[] AvailableThemes => [.. ThemeFiles.Keys];

    public void ApplyTheme(string themeName)
    {
        if (!ThemeFiles.TryGetValue(themeName, out var file)) return;

        var app = Application.Current;
        var mergedDicts = app.Resources.MergedDictionaries;

        var existing = mergedDicts.FirstOrDefault(d =>
        {
            var src = d.Source?.OriginalString;
            return src is not null
                   && src.Contains("Themes/", StringComparison.OrdinalIgnoreCase)
                   && !src.Contains("CartoTheme", StringComparison.OrdinalIgnoreCase);
        });
        if (existing is not null)
            mergedDicts.Remove(existing);

        mergedDicts.Insert(0, new ResourceDictionary { Source = new Uri(file, UriKind.Relative) });

        CurrentTheme = themeName;
    }

}
