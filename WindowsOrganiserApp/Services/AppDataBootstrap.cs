using System.IO;
using Serilog;
using SpecialAzerothService.Core.Models;
using SpecialAzerothService.Core.Services;

namespace WindowsOrganiserApp.Services;

/// <summary>Migration v4 et préparation des données avant le conteneur DI.</summary>
public static class AppDataBootstrap
{
    public static void Run(ILogger logger)
    {
        try
        {
            Directory.CreateDirectory(AppDataPaths.Directory);

            var settingsService = new SettingsService(logger);
            var cartoService = new CartoService();
            var settings = settingsService.Load();
            var carto = cartoService.Load();

            if (!CartoDataSchemaMigration.ApplyIfNeeded(settings, carto))
                return;

            cartoService.Save(carto);
            settingsService.Save(settings);
            logger.Information(
                "Migration données v{Version} appliquée (reset utilisateur).",
                CartoDataSchemaMigration.CurrentVersion);
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Échec migration données au démarrage");
        }
    }
}
