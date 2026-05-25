using WindowsOrganiserApp.Models.Carto;

namespace WindowsOrganiserApp.Services;

public static class CartoAccountSettings
{
    public static string ResolveDisplayName(
        string sourceFolder,
        IReadOnlyDictionary<string, CartoAccountConfig> settings)
    {
        if (string.IsNullOrWhiteSpace(sourceFolder))
            return sourceFolder;

        if (settings.TryGetValue(sourceFolder, out var cfg) && !string.IsNullOrWhiteSpace(cfg.DisplayName))
            return cfg.DisplayName.Trim();

        return sourceFolder;
    }

    public static AccountScope ResolveScope(
        string sourceFolder,
        IReadOnlyDictionary<string, CartoAccountConfig> settings)
    {
        if (settings.TryGetValue(sourceFolder, out var cfg))
            return cfg.Scope;
        return AccountScope.Mine;
    }

    public static string? ResolveUserId(
        string sourceFolder,
        IReadOnlyDictionary<string, CartoAccountConfig> settings,
        IReadOnlyList<CartoUser> users)
    {
        if (settings.TryGetValue(sourceFolder, out var cfg) && !string.IsNullOrWhiteSpace(cfg.UserId))
            return cfg.UserId;

        return users.FirstOrDefault(u =>
            u.Name.Equals(CartoUserMigration.DefaultUserName, StringComparison.OrdinalIgnoreCase))?.Id;
    }

    public static bool NamesMatch(
        string a,
        string b,
        IReadOnlyDictionary<string, CartoAccountConfig> settings)
    {
        if (a.Equals(b, StringComparison.OrdinalIgnoreCase))
            return true;

        var resolvedA = ResolveDisplayName(a, settings);
        var resolvedB = ResolveDisplayName(b, settings);

        return resolvedA.Equals(b, StringComparison.OrdinalIgnoreCase)
            || a.Equals(resolvedB, StringComparison.OrdinalIgnoreCase)
            || resolvedA.Equals(resolvedB, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Migre l'ancien accountDisplayNames vers accountSettings.</summary>
    public static void MigrateLegacyDisplayNames(CartoData data)
    {
        data.AccountSettings ??= new Dictionary<string, CartoAccountConfig>(StringComparer.OrdinalIgnoreCase);

        foreach (var (folder, display) in data.AccountDisplayNames)
        {
            if (data.AccountSettings.ContainsKey(folder))
                continue;

            data.AccountSettings[folder] = new CartoAccountConfig
            {
                DisplayName = display,
                Scope = AccountScope.Mine
            };
        }
    }

    public static Dictionary<string, CartoAccountConfig> SyncDictionaryFromDisplayNames(
        IReadOnlyDictionary<string, string> legacyNames)
    {
        var dict = new Dictionary<string, CartoAccountConfig>(StringComparer.OrdinalIgnoreCase);
        foreach (var (folder, display) in legacyNames)
        {
            dict[folder] = new CartoAccountConfig
            {
                DisplayName = display,
                Scope = AccountScope.Mine
            };
        }
        return dict;
    }
}
