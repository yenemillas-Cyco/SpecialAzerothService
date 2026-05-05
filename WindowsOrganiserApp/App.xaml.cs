using System.Diagnostics;
using System.IO;
using System.IO.Compression;
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
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IPresetService, PresetService>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<AdvancedViewModel>();
        services.AddTransient<MainWindow>();

        _serviceProvider = services.BuildServiceProvider();

        var mainVm = _serviceProvider.GetRequiredService<MainViewModel>();
        var advVm = _serviceProvider.GetRequiredService<AdvancedViewModel>();
        mainVm.AdvancedVm = advVm;

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
            http.Timeout = TimeSpan.FromMinutes(5);

            var release = await http.GetFromJsonAsync<GitHubRelease>(
                "https://api.github.com/repos/yenemillas-Cyco/SpecialAzerothService/releases/latest");

            if (release?.TagName == null || release.Assets == null) return;

            var remoteVer = new Version(release.TagName.TrimStart('v'));
            if (remoteVer <= CurrentVersion) return;

            var zipAsset = release.Assets.FirstOrDefault(a => a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
            if (zipAsset == null) return;

            var result = MessageBox.Show(
                $"Une nouvelle version (v{remoteVer}) est disponible !\n\nInstaller maintenant ?",
                "Mise à jour disponible",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result != MessageBoxResult.Yes) return;

            var tempZip = Path.Combine(Path.GetTempPath(), "SAS_update.zip");
            var tempDir = Path.Combine(Path.GetTempPath(), "SAS_update");

            await using (var stream = await http.GetStreamAsync(zipAsset.DownloadUrl))
            await using (var file = File.Create(tempZip))
                await stream.CopyToAsync(file);

            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            ZipFile.ExtractToDirectory(tempZip, tempDir);

            var newExe = Directory.GetFiles(tempDir, "*.exe", SearchOption.AllDirectories).FirstOrDefault();
            if (newExe == null) return;

            var currentExe = Environment.ProcessPath!;
            var currentPid = Environment.ProcessId;
            var batPath = Path.Combine(Path.GetTempPath(), "SAS_updater.bat");
            var bat = $"""
                @echo off
                echo Mise a jour en cours...
                taskkill /PID {currentPid} /F >nul 2>&1
                :retry
                timeout /t 2 /nobreak >nul
                copy /y "{newExe}" "{currentExe}" >nul 2>&1
                if errorlevel 1 goto retry
                del "{tempZip}" >nul 2>&1
                rmdir /s /q "{tempDir}" >nul 2>&1
                start "" "{currentExe}"
                del "%~f0"
                """;
            File.WriteAllText(batPath, bat);

            Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"{batPath}\"") { CreateNoWindow = true, UseShellExecute = false });
            Current.Shutdown();
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
    [property: JsonPropertyName("html_url")] string HtmlUrl,
    [property: JsonPropertyName("assets")] List<GitHubAsset>? Assets);

file record GitHubAsset(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("browser_download_url")] string DownloadUrl);
