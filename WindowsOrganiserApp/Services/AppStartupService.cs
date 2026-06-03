using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using WindowsOrganiserApp.ViewModels;

namespace WindowsOrganiserApp.Services;

public sealed class AppStartupService
{
    public static async Task RunAsync(IServiceProvider services, IProgress<StartupLoadProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        void Report(double percent, string message) =>
            progress?.Report(new StartupLoadProgress(percent, message));

        Report(5, "Initialisation…");
        await Task.Yield();

        var settingsService = services.GetRequiredService<ISettingsService>();
        var theme = services.GetRequiredService<IThemeService>();
        var appSettings = services.GetRequiredService<AppSettings>();

        Report(12, "Thème et paramètres…");
        var themeName = string.IsNullOrWhiteSpace(appSettings.Theme) ? "Classic" : appSettings.Theme;
        await Application.Current.Dispatcher.InvokeAsync(
            () => theme.ApplyTheme(themeName),
            DispatcherPriority.Send);

        var cartoVm = services.GetRequiredService<CartoViewModel>();
        await cartoVm.WarmupAsync(progress, cancellationToken).ConfigureAwait(false);

        Report(94, "Comptes craft…");
        var craftVm = services.GetRequiredService<CraftCraftingViewModel>();
        await craftVm.WarmupStockAccountsAsync(cancellationToken).ConfigureAwait(false);

        Report(100, "Prêt");
    }
}
