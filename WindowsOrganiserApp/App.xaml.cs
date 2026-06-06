using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using WindowsOrganiserApp.Controls;
using SpecialAzerothService.Core.Models;
using SpecialAzerothService.Core.Services;
using WindowsOrganiserApp.Services;
using WindowsOrganiserApp.ViewModels;
using WindowsOrganiserApp.Views;

namespace WindowsOrganiserApp;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;
    private static readonly Version CurrentVersion =
        typeof(App).Assembly.GetName().Version ?? new Version("1.0.0");

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                Log.Fatal(ex, "Exception fatale non gérée");
        };

        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error(args.Exception, "Exception UI non gérée : {Message}", args.Exception.ToString());
            // Ne pas bloquer l’UI pour les erreurs réseau Wowhead / tooltips
            if (IsNonCriticalUiException(args.Exception))
            {
                args.Handled = true;
                return;
            }

            if (args.Exception is NullReferenceException)
            {
                args.Handled = true;
                return;
            }

            var detail = args.Exception.InnerException?.Message ?? args.Exception.Message;
            MessageBox.Show(
                $"Erreur inattendue :\n{detail}",
                "Special Azeroth Service",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        KillOtherInstances();

        var logPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SpecialAzerothService", "logs", "app-.log");
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(logPath, rollingInterval: Serilog.RollingInterval.Day, retainedFileCountLimit: 7)
            .CreateLogger();

        // Avant DI : reset données locales si schéma < v4 (premier lancement 4.0.0).
        AppDataBootstrap.Run(Log.Logger);

        var services = new ServiceCollection();
        services.AddSingleton<ILogger>(Log.Logger);
        services.AddSingleton<IWindowService, WindowService>();
        services.AddSingleton<ILayoutService, LayoutService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<ICartoService, CartoService>();
        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<ISettingsService>().Load();
            var storedWow = WowGameRootStore.Read();
            if (!string.IsNullOrWhiteSpace(storedWow)
                && WowInstallPaths.TryCompleteUserFolder(storedWow, out var resolved))
            {
                settings.WowPath = resolved.GameRoot;
                sp.GetRequiredService<ISettingsService>().Save(settings);
            }

            return settings;
        });
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IPresetService, PresetService>();
        services.AddSingleton<ILocalizationService, LocalizationService>();
        services.AddSingleton<IBountyService, BountyService>();
        services.AddSingleton<IWowSyncService, WowSyncService>();
        services.AddSingleton<IWowheadDataService, WowheadDataService>();
        services.AddSingleton<IWowItemLookupService, WowItemLookupService>();
        services.AddSingleton<ICraftService, CraftService>();
        services.AddSingleton<ICraftListsService, CraftListsService>();
        services.AddSingleton<ICraftCatalogLookup, CraftCatalogLookup>();
        services.AddSingleton<ICraftDecompositionService, CraftDecompositionService>();
        services.AddSingleton<ICraftPickupPlanner, CraftPickupPlannerService>();
        services.AddSingleton<ICraftPlanningContext, CraftPlanningContext>();
        services.AddSingleton<ICraftStockService, CraftStockService>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<AdvancedViewModel>();
        services.AddSingleton<CartoViewModel>();
        services.AddSingleton<BountyViewModel>();
        services.AddSingleton<CraftViewModel>();
        services.AddSingleton<CraftCraftingViewModel>();
        services.AddSingleton<CraftShellViewModel>();
        services.AddTransient<MainWindow>();

        _serviceProvider = services.BuildServiceProvider();
        WowItemSlot.LookupService = _serviceProvider.GetRequiredService<IWowItemLookupService>();

        var mainVm = _serviceProvider.GetRequiredService<MainViewModel>();
        var advVm = _serviceProvider.GetRequiredService<AdvancedViewModel>();
        var cartoVm = _serviceProvider.GetRequiredService<CartoViewModel>();
        var bountyVm = _serviceProvider.GetRequiredService<BountyViewModel>();
        var craftShellVm = _serviceProvider.GetRequiredService<CraftShellViewModel>();
        mainVm.AdvancedVm = advVm;
        mainVm.CartoVm = cartoVm;
        mainVm.BountyVm = bountyVm;
        mainVm.CraftVm = craftShellVm;

        // Fenêtre principale assignée AVANT le splash : sinon la fermeture du splash
        // coupe l'app (ShutdownMode=OnMainWindowClose).
        MainWindow mainWindow;
        try
        {
            mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Échec création de la fenêtre principale");
            MessageBox.Show(
                $"Impossible de démarrer l'application :\n\n{ex.Message}",
                "Special Azeroth Service",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        var splash = new SplashWindow();
        splash.Show();

        var startupProgress = new Progress<StartupLoadProgress>(p =>
        {
            splash.Dispatcher.Invoke(() => splash.Report(p), DispatcherPriority.Render);
        });

        try
        {
            await AppStartupService.RunAsync(_serviceProvider, startupProgress);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Échec chargement au démarrage");
            splash.Close();
            MessageBox.Show(
                $"Erreur au chargement :\n\n{ex.Message}",
                "Special Azeroth Service",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        splash.Close();
        mainWindow.Show();
        mainWindow.Activate();
        if (mainWindow.WindowState == WindowState.Minimized)
            mainWindow.WindowState = WindowState.Normal;

        if (AppDataBootstrap.DataSchemaMigrationApplied)
        {
            MessageBox.Show(
                mainWindow,
                "Mise à jour v4.0.0 : votre configuration locale a été réinitialisée pour repartir sur une base propre.\n\n"
                + "• Effacé : paramètres, roster Carto, primes, listes craft, chemin WoW enregistré\n"
                + "• Conservé : calibration des zones sur la carte\n\n"
                + "Dans Carto → Paramètres, indiquez à nouveau le chemin WoW puis lancez un Rescan.",
                "Configuration réinitialisée",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

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
            var currentDir = Path.GetDirectoryName(currentExe)!;
            var sourceDir = Path.GetDirectoryName(newExe)!;
            var currentPid = Environment.ProcessId;
            var logFile = Path.Combine(Path.GetTempPath(), "SAS_updater.log");
            var batPath = Path.Combine(Path.GetTempPath(), "SAS_updater.bat");
            var bat = $"""
                @echo off
                echo Mise a jour en cours... > "{logFile}"
                taskkill /PID {currentPid} /F >nul 2>&1
                :retry
                timeout /t 2 /nobreak >nul
                echo Copie de "{sourceDir}" vers "{currentDir}" >> "{logFile}"
                robocopy "{sourceDir}" "{currentDir}" /E /IS /IT /NFL /NDL /NJH /NJS >> "{logFile}" 2>&1
                if %errorlevel% GEQ 8 (
                    echo Erreur robocopy errorlevel=%errorlevel% >> "{logFile}"
                    goto retry
                )
                echo Nettoyage... >> "{logFile}"
                del "{tempZip}" >nul 2>&1
                rmdir /s /q "{tempDir}" >nul 2>&1
                echo Lancement de "{currentExe}" >> "{logFile}"
                start "" "{currentExe}"
                del "%~f0"
                """;
            File.WriteAllText(batPath, bat);

            Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"{batPath}\"") { CreateNoWindow = true, UseShellExecute = false });
            Current.Shutdown();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Auto-update check failed");
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.CloseAndFlush();
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }

    private static void KillOtherInstances()
    {
        var currentPid = Environment.ProcessId;
        var currentName = Process.GetCurrentProcess().ProcessName;
        foreach (var proc in Process.GetProcessesByName(currentName))
        {
            if (proc.Id != currentPid)
            {
                try { proc.Kill(); } catch { }
            }
            proc.Dispose();
        }
    }

    private static bool IsNonCriticalUiException(Exception ex)
    {
        for (var e = ex; e != null; e = e.InnerException)
        {
            if (e is HttpRequestException or TaskCanceledException or OperationCanceledException)
                return true;
        }

        return false;
    }
}

file record GitHubRelease(
    [property: JsonPropertyName("tag_name")] string? TagName,
    [property: JsonPropertyName("html_url")] string HtmlUrl,
    [property: JsonPropertyName("assets")] List<GitHubAsset>? Assets);

file record GitHubAsset(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("browser_download_url")] string DownloadUrl);
