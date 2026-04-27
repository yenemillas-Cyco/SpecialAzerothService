using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using WindowsOrganiserApp.Services;
using WindowsOrganiserApp.ViewModels;

namespace WindowsOrganiserApp;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;
    private static readonly Version CurrentVersion =
        typeof(App).Assembly.GetName().Version ?? new Version("1.0.0");

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Fatal()
            .CreateLogger();

        var services = new ServiceCollection();
        services.AddSingleton<ILogger>(Log.Logger);
        services.AddSingleton<IWindowService, WindowService>();
        services.AddSingleton<ILayoutService, LayoutService>();
        services.AddTransient<MainViewModel>();
        services.AddTransient<MainWindow>();

        _serviceProvider = services.BuildServiceProvider();

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();

        await CheckForUpdatesAsync();
    }

    private static async Task CheckForUpdatesAsync()
    {
        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("SpecialAzerothService");
            http.Timeout = TimeSpan.FromSeconds(10);

            var release = await http.GetFromJsonAsync<GitHubRelease>(
                "https://api.github.com/repos/yenemillas-Cyco/SpecialAzerothService/releases/latest");

            if (release?.TagName == null) return;

            var remoteVer = new Version(release.TagName.TrimStart('v'));
            if (remoteVer <= CurrentVersion) return;

            var result = MessageBox.Show(
                $"Une nouvelle version (v{remoteVer}) est disponible !\n\nVoulez-vous ouvrir la page de téléchargement ?",
                "Mise à jour disponible",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
                Process.Start(new ProcessStartInfo(release.HtmlUrl) { UseShellExecute = true });
        }
        catch
        {
            // Pas d'internet ou erreur → on ignore silencieusement
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.CloseAndFlush();
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}

file record GitHubRelease(
    [property: JsonPropertyName("tag_name")] string? TagName,
    [property: JsonPropertyName("html_url")] string HtmlUrl);
