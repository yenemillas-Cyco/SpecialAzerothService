using Serilog;
using SpecialAzerothService.Core.Models.Carto;
using SpecialAzerothService.Core.Models.WowSync;
using SpecialAzerothService.Core.Services;

// Classifie les personnages WowSync dans carto.json (characterProfiles).
//
// Règles :
//   • Démoniste niveau 20 → TP Boy
//   • Niveau 1 sans banque → Clic Boys
//   • Niveau 1 avec objets en banque (WowSync) OU déjà catégorie Banque → Banque
//   • Tout le reste → Personnages (Main)
//
// Usage :
//   dotnet run --project tools/CartoBulkClassify          # aperçu
//   dotnet run --project tools/CartoBulkClassify -- --apply

var apply = args.Contains("--apply", StringComparer.OrdinalIgnoreCase);

var logger = new LoggerConfiguration().MinimumLevel.Fatal().CreateLogger();
var settingsService = new SettingsService(logger);
var cartoService = new CartoService();
var wowSync = new WowSyncService(settingsService, cartoService);

var carto = cartoService.Load();
var syncChars = wowSync
    .ReadAllAccounts(carto.AccountSettings)
    .SelectMany(a => a.Characters)
    .ToList();

if (syncChars.Count == 0)
{
    Console.Error.WriteLine("Aucun personnage WowSync trouvé. Vérifiez settings.json (wowPath) et WowSync.lua.");
    return 1;
}

var profilesByKey = (carto.CharacterProfiles ?? [])
    .Where(p => !string.IsNullOrWhiteSpace(p.SyncKey))
    .ToDictionary(p => p.SyncKey, StringComparer.OrdinalIgnoreCase);

var changes = new List<ChangeRow>();

foreach (var sync in syncChars.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase))
{
    var key = sync.Key;
    profilesByKey.TryGetValue(key, out var existing);
    var previous = existing?.Category ?? CharacterStatus.Reroll;
    var next = Classify(sync, previous);

    if (previous != next)
        changes.Add(new ChangeRow(sync, previous, next, ReasonLabel(sync, previous)));
}

Console.WriteLine($"Personnages WowSync : {syncChars.Count}");
Console.WriteLine($"Modifications prévues : {changes.Count}");
Console.WriteLine();

var byTarget = changes.GroupBy(c => c.Next).OrderBy(g => g.Key);
foreach (var g in byTarget)
{
    Console.WriteLine($"→ {CharacterStatusExtensions.DisplayName(g.Key)} ({g.Count()})");
    foreach (var row in g.OrderBy(r => r.Sync.Name))
        Console.WriteLine($"  • {row.Sync.Name} ({row.Sync.Class} {row.Sync.Level})  {Display(row.Previous)} → {Display(row.Next)}  [{row.Reason}]");
    Console.WriteLine();
}

if (!apply)
{
    Console.WriteLine("Mode aperçu — relancez avec --apply pour écrire carto.json (sauvegarde .bak créée).");
    return 0;
}

if (changes.Count == 0)
{
    Console.WriteLine("Rien à écrire.");
    return 0;
}

var appData = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "SpecialAzerothService");
var cartoPath = Path.Combine(appData, "carto.json");
if (File.Exists(cartoPath))
{
    var backup = cartoPath + $".bak-{DateTime.Now:yyyyMMdd-HHmmss}";
    File.Copy(cartoPath, backup, overwrite: false);
    Console.WriteLine($"Sauvegarde : {backup}");
}

foreach (var sync in syncChars)
{
    var key = sync.Key;
    var category = Classify(sync, profilesByKey.TryGetValue(key, out var ex) ? ex.Category : CharacterStatus.Reroll);

    if (profilesByKey.TryGetValue(key, out var profile))
        profile.Category = category;
    else
    {
        profile = new CartoCharacterProfile { SyncKey = key, Category = category };
        profilesByKey[key] = profile;
    }
}

carto.CharacterProfiles = profilesByKey.Values.OrderBy(p => p.SyncKey, StringComparer.OrdinalIgnoreCase).ToList();
cartoService.Save(carto);
MirrorDevCartoFile(cartoPath);

Console.WriteLine($"Écrit : {cartoPath} ({carto.CharacterProfiles.Count} profils)");
Console.WriteLine("Redémarrez l’app Carto pour recharger les catégories.");
return 0;

static CharacterStatus Classify(WowCharacterData sync, CharacterStatus previousCategory)
{
    if (sync.Level == 1)
    {
        if (HasBankContents(sync) || previousCategory == CharacterStatus.Banque)
            return CharacterStatus.Banque;
        return CharacterStatus.ClicBoys;
    }

    if (CartoSyncMapper.ParseClass(sync.Class) == WowClass.Demoniste && sync.Level == 20)
        return CharacterStatus.TpBoy;

    return CharacterStatus.Main;
}

static bool HasBankContents(WowCharacterData sync) =>
    sync.Bank.Any(i => i.Count > 0 || i.ItemId > 0 || !string.IsNullOrWhiteSpace(i.Name));

static string ReasonLabel(WowCharacterData sync, CharacterStatus previous)
{
    if (sync.Level == 1)
    {
        if (HasBankContents(sync))
            return "niv.1 + banque non vide";
        if (previous == CharacterStatus.Banque)
            return "niv.1 + déjà Banque";
        return "niv.1 sans banque";
    }

    if (CartoSyncMapper.ParseClass(sync.Class) == WowClass.Demoniste && sync.Level == 20)
        return "démoniste 20";

    return "défaut";
}

static string Display(CharacterStatus s) => CharacterStatusExtensions.DisplayName(s);

/// <summary>En build Debug, l'app lit <c>WindowsOrganiserApp/carto.user.json</c> — on y recopie les profils.</summary>
static void MirrorDevCartoFile(string appDataCartoPath)
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    for (var i = 0; i < 10 && dir != null; i++, dir = dir.Parent)
    {
        var devPath = Path.Combine(dir.FullName, "WindowsOrganiserApp", "carto.user.json");
        if (!File.Exists(devPath))
            continue;

        File.Copy(appDataCartoPath, devPath, overwrite: true);
        Console.WriteLine($"Copié vers (Debug) : {devPath}");
        return;
    }
}

sealed record ChangeRow(WowCharacterData Sync, CharacterStatus Previous, CharacterStatus Next, string Reason);
