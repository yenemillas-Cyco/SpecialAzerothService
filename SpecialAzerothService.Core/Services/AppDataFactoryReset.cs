using SpecialAzerothService.Core.Models;

using SpecialAzerothService.Core.Models.Carto;



namespace SpecialAzerothService.Core.Services;



/// <summary>Reset des données locales Carto / utilisateur.</summary>

public static class AppDataFactoryReset

{

    /// <summary>Factory reset complet (y compris calibration carte).</summary>

    public static void DeleteAllUserFiles()

    {

        DeleteUserLocalDataFiles();

        TryDelete(ZoneMapCalibration.FilePath);

        TryDelete(DungeonMarkerStore.FilePath);

    }



    /// <summary>

    /// Efface carto, settings, primes, craft, cache positions et presets.

    /// Conserve <see cref="ZoneMapCalibration"/> et <see cref="DungeonMarkerStore"/> (données carte, pas utilisateur).

    /// </summary>

    public static void DeleteUserLocalDataFiles()

    {

        TryDelete(AppDataPaths.SettingsFile);

        TryDelete(AppDataPaths.CartoFile);

        TryDelete(AppDataPaths.BountiesFile);

        TryDelete(AppDataPaths.CraftListsFile);

        TryDelete(CartoMapPositionStore.FilePath);
        WowGameRootStore.TryDelete();

        TryDeleteDirectory(AppDataPaths.PresetsDirectory);

    }



    /// <summary>Factory reset complet en mémoire (nouveau GUID).</summary>

    public static void ResetSettingsInPlace(AppSettings target)

    {

        var defaults = new AppSettings();

        target.WindowWidth = defaults.WindowWidth;

        target.WindowHeight = defaults.WindowHeight;

        target.WindowLeft = defaults.WindowLeft;

        target.WindowTop = defaults.WindowTop;

        target.Theme = defaults.Theme;

        target.Language = defaults.Language;

        target.WowPath = "";

        target.SyncDataMigrationVersion = 0;

        target.MonitorConfigs.Clear();

        target.Windows.Clear();

        target.DataSchemaVersion = CartoDataSchemaMigration.CurrentVersion;

    }



    public static void ResetCartoInPlace(CartoData data)

    {

        data.Users = [];

        data.CategoryPolicies = [];

        data.AccountSettings = new Dictionary<string, CartoAccountConfig>(StringComparer.OrdinalIgnoreCase);

        data.AccountDisplayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        data.Accounts = [];

        data.CharacterProfiles = [];

        data.CharacterExtras = [];

        data.Characters = [];

        data.Timers = [];

    }



    private static void TryDelete(string path)

    {

        try

        {

            if (File.Exists(path))

                File.Delete(path);

        }

        catch

        {

            // Non bloquant

        }

    }



    private static void TryDeleteDirectory(string path)

    {

        try

        {

            if (Directory.Exists(path))

                Directory.Delete(path, recursive: true);

        }

        catch

        {

            // Non bloquant

        }

    }

}


