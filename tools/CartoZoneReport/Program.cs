using System.Text.Json;
using SpecialAzerothService.Core.Models.WowSync;
using SpecialAzerothService.Core.Services;

var appData = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "SpecialAzerothService");

var settingsPath = Path.Combine(appData, "settings.json");
var settingsJson = File.ReadAllText(settingsPath);
var settings = JsonSerializer.Deserialize<SettingsDto>(settingsJson, new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true
}) ?? throw new InvalidOperationException("settings.json introuvable");

var wowPath = WowInstallPaths.NormalizeGameRoot(settings.WowPath ?? "");
var wtfRoot = WowInstallPaths.GetWtfAccountDirectory(wowPath);

ClassicEraMapProjection.ApplyUserRects(ZoneMapCalibration.LoadAll());

var syncChars = new List<(string Account, WowCharacterData Sync)>();
foreach (var dir in Directory.Exists(wtfRoot)
             ? Directory.GetDirectories(wtfRoot)
             : [])
{
    var account = Path.GetFileName(dir);
    var sv = Path.Combine(dir, "SavedVariables", "WowSync.lua");
    if (!File.Exists(sv))
        continue;

    foreach (var ch in ReadCharactersFromWowSync(sv))
        syncChars.Add((account, ch));
}

var refs = syncChars.Select(t => new CartoZoneCoverageAnalyzer.CharacterSyncRef
{
    SyncKey = t.Sync.Key,
    Name = t.Sync.Name,
    AccountName = t.Account,
    Sync = t.Sync
}).ToList();

var report = CartoZoneCoverageAnalyzer.Analyze(refs);

var cachePath = Path.Combine(appData, "map-positions-cache.json");
var cache = File.Exists(cachePath)
    ? JsonSerializer.Deserialize<MapCacheFile>(File.ReadAllText(cachePath))
    : null;

Console.WriteLine("=== RAPPORT ZONES CARTO (données locales) ===");
Console.WriteLine($"WoW: {wowPath}");
Console.WriteLine($"Personnages WowSync: {syncChars.Count}");
Console.WriteLine($"Rectangles calibrés (tous): {report.CalibratedZones.Count}");
Console.WriteLine($"  dont vos réglages (zone-calibration.json): {report.CalibratedZones.Count(z => z.IsUserPlaced)}");
Console.WriteLine();

Console.WriteLine($"--- ZONES MANQUANTES ({report.MissingZones.Count}) ---");
foreach (var z in report.MissingZones)
{
    Console.WriteLine($"• {z.ZoneDisplay} | map eff. {z.EffectiveMapId} (addon map {z.RawMapId})");
    Console.WriteLine($"  Persos ({z.CharacterCount}): {z.CharacterNames}");
    Console.WriteLine($"  Rectangle mapId connu: {(z.HasMapRectangle ? "oui" : "NON")} | Votre calibration: {(z.IsUserCalibrated ? "oui" : "non")}");
    Console.WriteLine();
}

var suspicious = CartoPlacementQuality.CollectSuspicious(
    syncChars.Select(t => (t.Sync.Key, t.Sync.Name, t.Account, t.Sync)));

Console.WriteLine($"--- PLACEMENTS DOUTEUX ({suspicious.Count}) — mer / map continent / hors zone ---");
foreach (var u in suspicious)
    Console.WriteLine($"  • {u.SummaryLine}");
Console.WriteLine();

Console.WriteLine($"--- PERSONNAGES NON PLAÇABLES ({report.UnplacedCharacters.Count}) ---");
foreach (var g in report.UnplacedCharacters.GroupBy(u => u.Reason))
{
    Console.WriteLine($"[{g.Key}] ({g.Count()})");
    foreach (var u in g.OrderBy(x => x.Name))
        Console.WriteLine($"  • {u.Name} ({u.AccountName}) — {u.ZoneDisplay} map {u.MapId} — {u.CoordsDisplay}");
    Console.WriteLine();
}

var coordsZero = new List<string>();
var onPile = new List<string>();
var capitalsOnWorld = new List<string>();
var cacheMismatch = new List<string>();
var instances = new List<string>();

foreach (var (account, sync) in syncChars)
{
    var hasCoords = sync.X > 0 || sync.Y > 0;
    var canProject = ClassicEraMapProjection.TryConvert(sync, out var projX, out var projY);
    var dungeon = CartoDungeonMarkerResolver.TryResolve(sync.Zone, sync.SubZone, out var dX, out var dY);
    var effMap = ClassicEraMapProjection.ResolveEffectiveMapId(sync.MapId, sync.Zone, sync.SubZone);

    if (!hasCoords)
    {
        coordsZero.Add($"{sync.Name} ({account}) — {sync.Zone} / {sync.SubZone}");
        continue;
    }

    if (!canProject && !dungeon)
    {
        instances.Add($"{sync.Name} ({account}) — {sync.Zone} / {sync.SubZone} (map {sync.MapId})");
        continue;
    }

    MapCacheEntry? cached = null;
    cache?.Entries.TryGetValue(sync.Key, out cached);
    var mapX = cached?.MapX ?? double.NaN;
    var mapY = cached?.MapY ?? double.NaN;
    var expectedX = canProject ? projX : dX;
    var expectedY = canProject ? projY : dY;

    if (!double.IsNaN(mapX) && CartoMapLayout.IsStackPosition(mapX, mapY))
    {
        onPile.Add($"{sync.Name} ({account}) — pile ({mapX:F3},{mapY:F3}) | WowSync: {sync.Zone} / {sync.SubZone} → ({expectedX:F3},{expectedY:F3})");
        continue;
    }

    if (canProject && ClassicEraMapProjection.IsCapitalMap(effMap))
    {
        capitalsOnWorld.Add($"{sync.Name} ({account}) — {sync.Zone} → carte monde ({projX:F3},{projY:F3})");
        continue;
    }

    if (cached?.Placed == true && canProject)
    {
        if (Math.Abs(mapX - expectedX) > 0.08 || Math.Abs(mapY - expectedY) > 0.08)
            cacheMismatch.Add($"{sync.Name} ({account}) — cache ({mapX:F3},{mapY:F3}) ≠ ({expectedX:F3},{expectedY:F3}) | {sync.Zone}");
    }
}

Console.WriteLine($"--- Coords addon à 0 ({coordsZero.Count}) ---");
foreach (var s in coordsZero.OrderBy(x => x)) Console.WriteLine("• " + s);
Console.WriteLine();

Console.WriteLine($"--- Sur la PILE (pas la vraie zone) ({onPile.Count}) ---");
foreach (var s in onPile.OrderBy(x => x)) Console.WriteLine("• " + s);
Console.WriteLine();

Console.WriteLine($"--- Capitales sur carte monde = « en mer » ({capitalsOnWorld.Count}) ---");
foreach (var s in capitalsOnWorld.OrderBy(x => x)) Console.WriteLine("• " + s);
Console.WriteLine();

Console.WriteLine($"--- Cache obsolète (≠ recalcul) ({cacheMismatch.Count}) ---");
foreach (var s in cacheMismatch.OrderBy(x => x)) Console.WriteLine("• " + s);
Console.WriteLine();

Console.WriteLine($"--- Instances / zone non projetée ({instances.Count}) ---");
foreach (var s in instances.OrderBy(x => x)) Console.WriteLine("• " + s);

Console.WriteLine();
Console.WriteLine("--- VOS RECTANGLES (zone-calibration.json) ---");
foreach (var z in report.CalibratedZones.Where(z => z.IsUserPlaced).OrderBy(z => z.DisplayName))
    Console.WriteLine($"  map {z.MapId} — {z.DisplayName}");

static List<WowCharacterData> ReadCharactersFromWowSync(string svFile)
{
    var list = new List<WowCharacterData>();
    var parsed = LuaTableParser.ParseFile(svFile);
    if (!parsed.TryGetValue("WowSyncDB", out var dbObj) || dbObj is not Dictionary<string, object?> db)
        return list;

    foreach (var (charKey, charValue) in db)
    {
        if (charValue is not Dictionary<string, object?> d)
            continue;

        var ch = new WowCharacterData
        {
            Name = LuaTableParser.GetString(d, "name"),
            Realm = LuaTableParser.GetString(d, "realm"),
            Level = LuaTableParser.GetInt(d, "level"),
            Class = LuaTableParser.GetString(d, "class"),
            Zone = LuaTableParser.GetString(d, "zone"),
            SubZone = LuaTableParser.GetString(d, "subZone"),
            X = LuaTableParser.GetDouble(d, "x"),
            Y = LuaTableParser.GetDouble(d, "y"),
            MapId = LuaTableParser.GetInt(d, "mapId"),
            StorageKey = charKey.Trim()
        };
        list.Add(ch);
    }

    return list;
}

sealed class SettingsDto
{
    public string? WowPath { get; set; }
}

sealed class MapCacheFile
{
    public Dictionary<string, MapCacheEntry> Entries { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

sealed class MapCacheEntry
{
    public double MapX { get; set; }
    public double MapY { get; set; }
    public string? SourceStamp { get; set; }
    public bool Placed { get; set; }
}
