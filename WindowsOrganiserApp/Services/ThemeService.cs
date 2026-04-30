using System.Windows;

namespace WindowsOrganiserApp.Services;

public interface IThemeService
{
    string CurrentTheme { get; }
    string[] AvailableThemes { get; }
    void ApplyTheme(string themeName);
    void CycleTheme();
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

        mergedDicts.Clear();
        mergedDicts.Add(new ResourceDictionary { Source = new Uri(file, UriKind.Relative) });

        CurrentTheme = themeName;
    }

    public void CycleTheme()
    {
        var keys = AvailableThemes;
        var idx = Array.IndexOf(keys, CurrentTheme);
        var next = keys[(idx + 1) % keys.Length];
        ApplyTheme(next);
    }
}
