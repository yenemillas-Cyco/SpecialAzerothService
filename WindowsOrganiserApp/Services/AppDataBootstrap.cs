using System.IO;
using Serilog;
using SpecialAzerothService.Core.Models;
using SpecialAzerothService.Core.Services;

namespace WindowsOrganiserApp.Services;

/// <summary>Migration schéma données avant le conteneur DI (reset v4 au premier lancement 4.0.0).</summary>
public static class AppDataBootstrap
{
    /// <summary>True si un reset schéma vient d'être appliqué à ce lancement.</summary>
    public static bool DataSchemaMigrationApplied { get; private set; }

    public static void Run(ILogger logger)
    {
        DataSchemaMigrationApplied = false;
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
            DataSchemaMigrationApplied = true;
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
