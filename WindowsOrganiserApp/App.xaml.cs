using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Velopack;
using Velopack.Sources;
using WindowsOrganiserApp.Services;
using WindowsOrganiserApp.ViewModels;

namespace WindowsOrganiserApp;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        VelopackApp.Build().Run();

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
            var source = new GithubSource("https://github.com/yenemillas-Cyco/SpecialAzerothService", null, false);
            var mgr = new UpdateManager(source);

            if (!mgr.IsInstalled) return;

            var newVersion = await mgr.CheckForUpdatesAsync();
            if (newVersion == null) return;

            await mgr.DownloadUpdatesAsync(newVersion);

            var result = MessageBox.Show(
                $"Une nouvelle version ({newVersion.TargetFullRelease.Version}) est disponible.\nRedémarrer maintenant ?",
                "Mise à jour",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
                mgr.ApplyUpdatesAndRestart(newVersion);
        }
        catch
        {
            // Silently ignore update errors (no internet, repo not found, etc.)
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.CloseAndFlush();
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
