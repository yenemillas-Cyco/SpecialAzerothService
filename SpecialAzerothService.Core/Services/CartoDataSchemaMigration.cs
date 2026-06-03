using SpecialAzerothService.Core.Models;

using SpecialAzerothService.Core.Models.Carto;



namespace SpecialAzerothService.Core.Services;



/// <summary>

/// Migration des données locales. Incrémenter <see cref="CurrentVersion"/> pour forcer un reset ciblé.

/// v4 : version locale — efface toutes les données utilisateur, conserve calibration carte (zones, lieux-dits).

/// </summary>

public static class CartoDataSchemaMigration

{

    public const int CurrentVersion = 4;



    public static bool ApplyIfNeeded(AppSettings settings, CartoData data)

    {

        MigrateLegacyVersionField(settings);



        if (settings.DataSchemaVersion >= CurrentVersion)

            return false;



        AppDataFactoryReset.DeleteUserLocalDataFiles();

        AppDataFactoryReset.ResetSettingsInPlace(settings);

        AppDataFactoryReset.ResetCartoInPlace(data);

        return true;

    }



    private static void MigrateLegacyVersionField(AppSettings settings)

    {

        if (settings.DataSchemaVersion > 0)

            return;



        if (settings.SyncDataMigrationVersion > 0)

            settings.DataSchemaVersion = 1;

    }

}


