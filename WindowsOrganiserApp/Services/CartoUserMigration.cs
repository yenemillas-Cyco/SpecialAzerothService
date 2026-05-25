using WindowsOrganiserApp.Models.Carto;

namespace WindowsOrganiserApp.Services;

public static class CartoUserMigration
{
    public const string DefaultUserName = "Moi";
    public const string EloiUserName = "Eloi";
    public const string LuckyUserName = "Lucky";

    public const string LuckyAccountFolder = "409878243#1";

    /// <summary>Comptes WTF rattachés à l'utilisateur Eloi (Harry est un compte, pas un utilisateur).</summary>
    private static readonly HashSet<string> EloiAccountNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "harry", "wow1", "wow2", "wow3", "HARRYKENLER", "Harrykenler"
    };

    /// <summary>Dossiers fantômes sans persos WowSync (doublon nom affiché « Lucky », etc.).</summary>
    public static readonly HashSet<string> GhostAccountFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Lucky"
    };

    public static bool IsHarryWtfFolder(string? folder) =>
        !string.IsNullOrWhiteSpace(folder)
        && folder.Contains("harry", StringComparison.OrdinalIgnoreCase);

    /// <summary>Utilisateur par défaut pour un nouveau dossier WTF (ex. HARRYKENLER → Eloi).</summary>
    public static string? ResolveDefaultUserIdForFolder(string sourceFolder, CartoData data)
    {
        if (IsHarryWtfFolder(sourceFolder))
        {
            var eloi = data.Users.FirstOrDefault(u =>
                u.Name.Equals(EloiUserName, StringComparison.OrdinalIgnoreCase));
            if (eloi != null)
                return eloi.Id;
        }

        return data.Users.FirstOrDefault(u =>
            u.Name.Equals(DefaultUserName, StringComparison.OrdinalIgnoreCase))?.Id
            ?? data.Users.OrderBy(u => u.SortOrder).FirstOrDefault()?.Id;
    }

    public static void Migrate(CartoData data)
    {
        data.Users ??= [];
        data.CategoryPolicies ??= [];
        data.AccountSettings ??= new Dictionary<string, CartoAccountConfig>(StringComparer.OrdinalIgnoreCase);

        var usersByName = data.Users
            .GroupBy(u => u.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        CartoUser EnsureUser(string name, int sortOrder)
        {
            if (usersByName.TryGetValue(name, out var existing))
                return existing;

            var user = new CartoUser { Name = name, SortOrder = sortOrder };
            data.Users.Add(user);
            usersByName[name] = user;
            return user;
        }

        var moi = EnsureUser(DefaultUserName, 0);
        var eloi = usersByName.GetValueOrDefault(EloiUserName);
        var lucky = EnsureUser(LuckyUserName, 2);

        foreach (var cfg in data.AccountSettings.Values)
        {
            if (!string.IsNullOrWhiteSpace(cfg.UserId)
                && data.Users.Any(u => u.Id == cfg.UserId))
                continue;

            var target = ResolveUserForLegacyConfig(cfg, moi, eloi, lucky, EnsureUser);
            cfg.UserId = target.Id;
        }

        if (eloi != null)
        {
            ConsolidateHarryUserIntoEloi(data, eloi);
            ApplyKnownAccountAssignments(data, eloi, lucky);
        }
        else
        {
            ApplyKnownAccountAssignments(data, null, lucky);
        }

        foreach (var cfg in data.AccountSettings.Values)
        {
            if (string.IsNullOrWhiteSpace(cfg.UserId)
                || !data.Users.Any(u => u.Id == cfg.UserId))
                cfg.UserId = moi.Id;
        }

        ReindexUsers(data);
        MigrateRerollIntoMain(data);
        CleanupAccounts(data);
    }

    /// <summary>Supprime les comptes vides (Lucky / Harrykenler fantômes) et renomme 409878243#1 → Lucky.</summary>
    public static void CleanupAccounts(
        CartoData data,
        IReadOnlyDictionary<string, int>? characterCountByFolder = null)
    {
        foreach (var folder in data.AccountSettings.Keys.ToList())
        {
            if (!ShouldRemoveGhostFolder(folder, characterCountByFolder))
                continue;

            RemoveAccountFolder(data, folder);
        }

        FixLuckyAccountAndUser(data);
        RemoveOrphanUsers(data);
        CleanupCategoryPoliciesForMissingUsers(data);
        ReindexUsers(data);
    }

    private static bool ShouldRemoveGhostFolder(
        string folder,
        IReadOnlyDictionary<string, int>? characterCountByFolder)
    {
        if (folder.Equals(LuckyAccountFolder, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!GhostAccountFolders.Contains(folder))
            return false;

        if (characterCountByFolder == null)
            return true;

        return !characterCountByFolder.TryGetValue(folder, out var count) || count == 0;
    }

    private static void RemoveAccountFolder(CartoData data, string folder)
    {
        data.AccountSettings.Remove(folder);
        data.AccountDisplayNames.Remove(folder);
        data.Accounts.RemoveAll(a =>
            !string.IsNullOrEmpty(a.SourceFolder)
            && a.SourceFolder.Equals(folder, StringComparison.OrdinalIgnoreCase));
    }

    private static void FixLuckyAccountAndUser(CartoData data)
    {
        if (!data.AccountSettings.TryGetValue(LuckyAccountFolder, out var cfg))
            return;

        var luckyUser = EnsureCanonicalLuckyUser(data);
        if (string.IsNullOrWhiteSpace(cfg.DisplayName)
            || cfg.DisplayName.Equals(LuckyAccountFolder, StringComparison.OrdinalIgnoreCase))
            cfg.DisplayName = LuckyUserName;

        cfg.FriendLabel = null;
        cfg.Scope = AccountScope.Mine;

        if (string.IsNullOrWhiteSpace(cfg.UserId)
            || !data.Users.Any(u => u.Id == cfg.UserId))
            cfg.UserId = luckyUser.Id;

        data.AccountDisplayNames[LuckyAccountFolder] = cfg.DisplayName;

        var account = data.Accounts.FirstOrDefault(a =>
            LuckyAccountFolder.Equals(a.SourceFolder, StringComparison.OrdinalIgnoreCase));
        if (account != null)
            account.Name = LuckyUserName;
    }

    private static CartoUser EnsureCanonicalLuckyUser(CartoData data)
    {
        var folderNamedUser = data.Users.FirstOrDefault(u =>
            u.Name.Equals(LuckyAccountFolder, StringComparison.OrdinalIgnoreCase));
        var luckyNamedUser = data.Users.FirstOrDefault(u =>
            u.Name.Equals(LuckyUserName, StringComparison.OrdinalIgnoreCase));

        if (folderNamedUser != null)
        {
            if (luckyNamedUser != null && luckyNamedUser.Id != folderNamedUser.Id)
            {
                ReassignUserId(data, luckyNamedUser.Id, folderNamedUser.Id);
                data.Users.Remove(luckyNamedUser);
            }

            folderNamedUser.Name = LuckyUserName;
            return folderNamedUser;
        }

        if (luckyNamedUser != null)
            return luckyNamedUser;

        var created = new CartoUser { Name = LuckyUserName, SortOrder = 2 };
        data.Users.Add(created);
        return created;
    }

    private static void RemoveOrphanUsers(CartoData data)
    {
        var referenced = data.AccountSettings.Values
            .Select(c => c.UserId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var ghosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "HARRYKENLER", "Harrykenler", "Harry", LuckyUserName
        };

        foreach (var user in data.Users.ToList())
        {
            if (user.Name.Equals(DefaultUserName, StringComparison.OrdinalIgnoreCase))
                continue;

            if (user.Name.Equals(LuckyUserName, StringComparison.OrdinalIgnoreCase)
                && referenced.Contains(user.Id))
                continue;

            if (referenced.Contains(user.Id))
                continue;

            if (ghosts.Contains(user.Name) || user.Name.Equals(LuckyAccountFolder, StringComparison.OrdinalIgnoreCase))
                data.Users.Remove(user);
        }
    }

    private static void ReassignUserId(CartoData data, string fromId, string toId)
    {
        foreach (var cfg in data.AccountSettings.Values)
        {
            if (cfg.UserId == fromId)
                cfg.UserId = toId;
        }

        foreach (var policy in data.CategoryPolicies)
        {
            if (policy.UserId == fromId)
                policy.UserId = toId;
        }
    }

    private static void CleanupCategoryPoliciesForMissingUsers(CartoData data)
    {
        var validUserIds = data.Users.Select(u => u.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        data.CategoryPolicies.RemoveAll(p => !validUserIds.Contains(p.UserId));
    }

    /// <summary>Fusionne la catégorie Reroll dans Personnages (Main).</summary>
    public static void MigrateRerollIntoMain(CartoData data)
    {
        foreach (var profile in data.CharacterProfiles)
        {
            if (profile.Category == CharacterStatus.Reroll)
                profile.Category = CharacterStatus.Main;
        }

        foreach (var extra in data.CharacterExtras)
        {
            if (extra.Status == CharacterStatus.Reroll)
                extra.Status = CharacterStatus.Main;
        }

        foreach (var ch in data.Characters.Concat(data.ExternalCharacters))
        {
            if (ch.Status == CharacterStatus.Reroll)
                ch.Status = CharacterStatus.Main;
        }

        var rerollPolicies = data.CategoryPolicies
            .Where(p => p.Category == CharacterStatus.Reroll)
            .ToList();
        foreach (var reroll in rerollPolicies)
        {
            if (!data.CategoryPolicies.Any(p =>
                    p.UserId == reroll.UserId && p.Category == CharacterStatus.Main))
                reroll.Category = CharacterStatus.Main;
            else
                data.CategoryPolicies.Remove(reroll);
        }
    }

    private static CartoUser ResolveUserForLegacyConfig(
        CartoAccountConfig cfg,
        CartoUser moi,
        CartoUser? eloi,
        CartoUser lucky,
        Func<string, int, CartoUser> ensureUser)
    {
        var display = cfg.DisplayName.Trim();
        var label = cfg.FriendLabel?.Trim() ?? "";

        if (IsLuckylliasAccount(display, label))
            return lucky;

        if (BelongsToEloi(display, label))
            return eloi ?? moi;

        if (cfg.Scope == AccountScope.Friend)
        {
            if (!string.IsNullOrWhiteSpace(label) && !label.Equals("Harry", StringComparison.OrdinalIgnoreCase))
                return ensureUser(label, 10);

            if (!string.IsNullOrWhiteSpace(display) && !EloiAccountNames.Contains(display))
                return ensureUser(display, 10);

            return eloi ?? moi;
        }

        return moi;
    }

    private static bool BelongsToEloi(string display, string label) =>
        label.Equals("Harry", StringComparison.OrdinalIgnoreCase)
        || EloiAccountNames.Contains(display);

    private static bool IsLuckylliasAccount(string display, string label) =>
        display.Contains("luckyllias", StringComparison.OrdinalIgnoreCase)
        || label.Contains("luckyllias", StringComparison.OrdinalIgnoreCase);

    /// <summary>Fusionne l'utilisateur « Harry » créé par erreur dans Eloi.</summary>
    private static void ConsolidateHarryUserIntoEloi(CartoData data, CartoUser eloi)
    {
        var harryUsers = data.Users
            .Where(u => u.Name.Equals("Harry", StringComparison.OrdinalIgnoreCase)
                        && u.Id != eloi.Id)
            .ToList();

        foreach (var harryUser in harryUsers)
        {
            foreach (var cfg in data.AccountSettings.Values)
            {
                if (cfg.UserId == harryUser.Id)
                    cfg.UserId = eloi.Id;
            }

            data.Users.Remove(harryUser);
        }
    }

    private static void ApplyKnownAccountAssignments(CartoData data, CartoUser? eloi, CartoUser lucky)
    {
        foreach (var (folder, cfg) in data.AccountSettings)
        {
            var display = cfg.DisplayName.Trim();
            var hasValidUser = !string.IsNullOrWhiteSpace(cfg.UserId)
                && data.Users.Any(u => u.Id == cfg.UserId);

            var isLuckyFolder = folder.Equals(LuckyAccountFolder, StringComparison.OrdinalIgnoreCase)
                || IsLuckylliasAccount(display, cfg.FriendLabel ?? "")
                || folder.Contains("luckyllias", StringComparison.OrdinalIgnoreCase);

            if (isLuckyFolder)
            {
                // Ne pas écraser un rattachement déjà choisi par l'utilisateur (sauvegardé dans carto.json).
                if (!hasValidUser)
                    cfg.UserId = lucky.Id;

                if (folder.Equals(LuckyAccountFolder, StringComparison.OrdinalIgnoreCase)
                    && (string.IsNullOrWhiteSpace(cfg.DisplayName)
                        || cfg.DisplayName.Equals(LuckyAccountFolder, StringComparison.OrdinalIgnoreCase)))
                    cfg.DisplayName = LuckyUserName;

                continue;
            }

            if (hasValidUser)
                continue;

            if (eloi != null
                && (BelongsToEloi(display, cfg.FriendLabel ?? "")
                    || IsHarryWtfFolder(folder)))
                cfg.UserId = eloi.Id;
        }
    }

    public static void ReindexUsers(CartoData data)
    {
        var order = 0;
        foreach (var user in data.Users
                     .OrderBy(u => u.Name.Equals(DefaultUserName, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                     .ThenBy(u => u.SortOrder)
                     .ThenBy(u => u.Name, StringComparer.OrdinalIgnoreCase))
            user.SortOrder = order++;
    }

    public static void ApplyLegacyAccountHiddenToCharacters(
        CartoData data,
        IEnumerable<WowCharacter> characters)
    {
        var hiddenFolders = data.AccountSettings
            .Where(kv => kv.Value.IsHiddenOnMap)
            .Select(kv => kv.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var ch in characters)
        {
            if (ch.IsHidden)
                continue;

            var account = data.Accounts.FirstOrDefault(a => a.Id == ch.AccountId);
            if (account == null)
                continue;

            var folder = account.SourceFolder;
            if (!string.IsNullOrWhiteSpace(folder) && hiddenFolders.Contains(folder))
                ch.IsHidden = true;
            else if (account.IsHidden)
                ch.IsHidden = true;
        }
    }
}
