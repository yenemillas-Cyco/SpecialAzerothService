using SpecialAzerothService.Core.Models.Carto;
using SpecialAzerothService.Core.Models.WowSync;

namespace SpecialAzerothService.Core.Services;

/// <summary>
/// Classic Era : convertit (mapId, x, y) jeu → (MapX, MapY) sur Assets/WowMap.png (0–1).
/// </summary>
public static class ClassicEraMapProjection
{
    public readonly record struct CartoMapRect(double Left, double Top, double Width, double Height);

    public readonly record struct CartoMapPoint(double X, double Y);

    private readonly record struct ZoneMapEntry(
        int MapId,
        string ZoneNameEn,
        double Width,
        double Height,
        double? Left = null,
        double? Top = null);

    private static readonly Dictionary<int, CartoMapRect> ByMapId = new();
    private static readonly Dictionary<int, CartoMapRect> BuiltInZoneRects = new();
    private static readonly Dictionary<int, CartoMapRect> ContinentRects = new()
    {
        // Continent : même repère haut-gauche que les cartes zone in-game
        [1414] = new(0.030, 0.020, 0.380, 0.620), // Kalimdor
        [1415] = new(0.550, 0.020, 0.380, 0.650), // Royaumes de l'Est
    };

    private static readonly HashSet<string> ContinentZoneLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        "kalimdor", "royaumes de l est", "eastern kingdoms", "azeroth"
    };

    private static readonly List<(string NameEn, string NameFr, CartoMapRect Rect)> ByZoneName = [];
    private static readonly Dictionary<string, int> ZoneNameToMapId = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Villes / hubs de vol → UiMapID de la zone parente (coords 0–1 relatives à cette zone).</summary>
    private static readonly Dictionary<string, int> HubToParentZoneMapId = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Noms de zone FR/EN → UiMapID (Classic Era).</summary>
    private static readonly Dictionary<string, int> ZoneAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["clairieres de tirisfal"] = 1420,
        ["tirisfal"] = 1420,
        ["maleterres de l'est"] = 1423,
        ["maleterres de l est"] = 1423,
        ["maleterres de l'ouest"] = 1422,
        ["maleterres de l ouest"] = 1422,
        ["cratere d'ungoro"] = 1449,
        ["cratere d ungoro"] = 1449,
        ["ungoro"] = 1449,
        ["les tarides"] = 1413,
        ["tarides"] = 1413,
        ["terres foudroyees"] = 1419,
        ["marais des chagrins"] = 1435,
        ["vallee de strangleronce"] = 1434,
        ["strangleronce"] = 1434,
        ["les paluns"] = 1437,
        ["mille pointes"] = 1441,
        ["sombrivage"] = 1439,
        ["orneval"] = 1440,
        ["felwood"] = 1448,
        ["gangrebois"] = 1448,
        ["silithus"] = 1451,
        ["fort cenarien"] = 1451,
        ["cenarion hold"] = 1451,
        ["refuge du marechal"] = 1449,
        ["croisee"] = 1413,
        ["auberdine"] = 1439,
        ["orgrimmar"] = 1454,
        ["fossoyeuse"] = 1458,
        ["hurlevent"] = 1453,
        ["forgefer"] = 1455,
        ["pitons du tonnerre"] = 1456,
        ["darnassus"] = 1457,
        ["refuge du marechal"] = 1449,
        ["gadgetzan"] = 1446,
        ["long guet"] = 1452,
        ["desolace"] = 1443,
        ["feralas"] = 1444,
        ["durotar"] = 1411,
        ["mulgore"] = 1412,
        ["sombrivage"] = 1439,
        ["teldrassil"] = 1438,
        ["tanaris"] = 1446,
        ["terres ingrates"] = 1418,
        ["badlands"] = 1418,
        ["blasted lands"] = 1419,
        ["silverpine forest"] = 1421,
        ["foret des pins argentes"] = 1421,
        ["the barrens"] = 1413,
        ["les tarides"] = 1413,
        ["winterspring"] = 1452,
        ["berceau de l hiver"] = 1452,
        ["stormwind city"] = 1453,
        ["cite de stormwind"] = 1453,
        ["vallee de la force"] = 1454,
        ["cleft of shadow"] = 1458,
        ["vallee des esprits"] = 1456,
        ["azshara"] = 1447,
        ["mille pointes"] = 1441,
        ["serres rocheuses"] = 1442,
        ["reflet de lune"] = 1450,
        ["berceau de l hiver"] = 1452,
        // Mont Rochenoire / donjons BR (entrée côté Steppes Ardentes)
        ["mont rochenoire"] = 1428,
        ["mont blackrock"] = 1428,
        ["blackrock mountain"] = 1428,
        ["blackrock"] = 1428,
        ["le viaduc du magma"] = 1428,
        ["viaduc du magma"] = 1428,
        ["gorge des vents brulants"] = 1427,
        ["searing gorge"] = 1427,
        ["steppes ardentes"] = 1428,
        ["burning steppes"] = 1428,
        ["profondeurs de rochenoire"] = 1428,
        ["blackrock depths"] = 1428,
        ["brd"] = 1428,
        ["coeur du magma"] = 1428,
        ["molten core"] = 1428,
        ["repaire de l aile noire"] = 1428,
        ["blackwing lair"] = 1428,
        ["pic de rochenoire"] = 1428,
        ["lower blackrock spire"] = 1428,
        ["upper blackrock spire"] = 1428,
        // Donjons → zone monde parente (entrée / zone affichée in-game)
        ["gouffre de ragefeu"] = 1411,
        ["ragefire chasm"] = 1411,
        ["mortemines"] = 1436,
        ["deadmines"] = 1436,
        ["cavernes des lamentations"] = 1413,
        ["wailing caverns"] = 1413,
        ["ombrecroc"] = 1421,
        ["shadowfang keep"] = 1421,
        ["la prison"] = 1453,
        ["stockades"] = 1453,
        ["brassenoire"] = 1440,
        ["blackfathom deeps"] = 1440,
        ["tranchebauge"] = 1413,
        ["razorfen"] = 1413,
        ["gnomeragan"] = 1426,
        ["monastere ecarlate"] = 1420,
        ["scarlet monastery"] = 1420,
        ["uldaman"] = 1418,
        ["zul farrak"] = 1446,
        ["maraudon"] = 1443,
        ["atal hakkar"] = 1435,
        ["sunken temple"] = 1435,
        ["haches tripes"] = 1444,
        ["dire maul"] = 1444,
        ["stratholme"] = 1423,
        ["scholomance"] = 1422,
        ["zul gurub"] = 1434,
        ["repere d onyxia"] = 1445,
        ["onyxia"] = 1445,
        ["ruines d ahn qiraj"] = 1451,
        ["temple d ahn qiraj"] = 1451,
        ["portes d ahn qiraj"] = 1451,
        ["gates of ahn qiraj"] = 1451,
        ["naxxramas"] = 1423,
    };

    private static readonly HashSet<int> CityMapIds =
    [
        1453, 1454, 1455, 1456, 1457, 1458, 1450
    ];

    /// <summary>Capitales — libellé anglais dans le gestionnaire de zones.</summary>
    private static readonly HashSet<int> CapitalMapIds =
    [
        1453, 1454, 1455, 1456, 1457, 1458
    ];

    public static bool IsCapitalMap(int mapId) => CapitalMapIds.Contains(mapId);

    static ClassicEraMapProjection()
    {
        foreach (var (alias, mapId) in ZoneAliases)
        {
            if (!IsContinentMap(mapId))
                HubToParentZoneMapId[alias] = mapId;
        }

        foreach (var entry in ZoneEntries)
        {
            var zone = MapOverlayData.Zones.FirstOrDefault(z =>
                z.NameEN.Equals(entry.ZoneNameEn, StringComparison.OrdinalIgnoreCase));
            if (zone == null) continue;

            var rect = entry.Left.HasValue && entry.Top.HasValue
                ? new CartoMapRect(entry.Left.Value, entry.Top.Value, entry.Width, entry.Height)
                : CapitalMapIds.Contains(entry.MapId)
                    // Capitales : rectangle assez grand pour les coords 0–1 in-game (sinon tout le monde au centre).
                    ? RectAt(zone.X, zone.Y, 0.058, 0.072)
                    : RectAt(zone.X, zone.Y, entry.Width * 0.65, entry.Height * 0.65);
            ByMapId[entry.MapId] = rect;

            var enKey = NormalizeZoneName(zone.NameEN);
            var frKey = NormalizeZoneName(zone.NameFR);
            ZoneNameToMapId[enKey] = entry.MapId;
            ZoneNameToMapId[frKey] = entry.MapId;
            ZoneNameToMapId[NormalizeZoneName(entry.ZoneNameEn)] = entry.MapId;
        }

        foreach (var (alias, id) in ZoneAliases)
            ZoneNameToMapId[NormalizeZoneName(alias)] = id;

        // Calibrations fixes (priorité sur zone-calibration.json). Origine zone = haut-gauche.
        ByMapId[1443] = CalibrateFromTwoPoints(
            0.266, 0.598, 0.060, 0.420,
            0.647, 0.106, 0.080, 0.365);
        ByMapId[1451] = new CartoMapRect(0.094, 0.497, 0.075, 0.085);

        BuiltInZoneRects.Clear();
        foreach (var (mapId, rect) in ByMapId)
        {
            if (!IsContinentMap(mapId))
                BuiltInZoneRects[mapId] = rect;
        }

        ZoneMapCalibration.ApplyOverrides(ByMapId);

        RebuildByZoneNameRects();
    }

    /// <summary>Rectangles par défaut (code + Assets/zone-rects-classic-era.json), avant calibration utilisateur.</summary>
    public static IReadOnlyDictionary<int, CartoMapRect> GetBuiltInZoneRects() => BuiltInZoneRects;

    private static void RebuildByZoneNameRects()
    {
        ByZoneName.Clear();
        foreach (var entry in ZoneEntries)
        {
            var zone = MapOverlayData.Zones.FirstOrDefault(z =>
                z.NameEN.Equals(entry.ZoneNameEn, StringComparison.OrdinalIgnoreCase));
            if (zone == null || !ByMapId.TryGetValue(entry.MapId, out var rect)) continue;
            ByZoneName.Add((zone.NameEN, zone.NameFR, rect));
        }
    }

    private static readonly ZoneMapEntry[] ZoneEntries =
    [
        // Kalimdor — tailles de base (×0,65 appliqué sauf Left/Top explicites)
        new(1411, "Durotar", 0.100, 0.120),
        new(1412, "Mulgore", 0.105, 0.120),
        new(1413, "The Barrens", 0.180, 0.180),
        new(1438, "Teldrassil", 0.090, 0.105),
        new(1439, "Darkshore", 0.120, 0.135),
        new(1440, "Ashenvale", 0.135, 0.150),
        new(1441, "Thousand Needles", 0.145, 0.135),
        new(1442, "Stonetalon Mountains", 0.120, 0.128),
        new(1443, "Desolace", 0.128, 0.135), // remplacé par CalibrateFromTwoPoints en static ctor
        new(1444, "Feralas", 0.168, 0.175),
        new(1445, "Dustwallow Marsh", 0.145, 0.150),
        new(1446, "Tanaris", 0.150, 0.150),
        new(1447, "Azshara", 0.128, 0.145),
        new(1448, "Felwood", 0.128, 0.135),
        new(1449, "Un'Goro Crater", 0.135, 0.145),
        new(1450, "Moonglade", 0.080, 0.088),
        new(1451, "Silithus", 0.075, 0.085),
        new(1452, "Winterspring", 0.150, 0.150),
        new(1454, "Orgrimmar", 0.045, 0.045),
        new(1456, "Thunder Bluff", 0.045, 0.045),
        new(1457, "Darnassus", 0.042, 0.042),
        // Royaumes de l'Est
        new(1416, "Alterac Mountains", 0.112, 0.120),
        new(1417, "Arathi Highlands", 0.135, 0.145),
        new(1418, "Badlands", 0.128, 0.135),
        new(1419, "Blasted Lands", 0.135, 0.145),
        new(1420, "Tirisfal Glades", 0.096, 0.112),
        new(1421, "Silverpine Forest", 0.120, 0.128),
        new(1422, "Western Plaguelands", 0.150, 0.135),
        new(1423, "Eastern Plaguelands", 0.160, 0.150),
        new(1424, "Hillsbrad Foothills", 0.120, 0.128),
        new(1425, "The Hinterlands", 0.135, 0.145),
        new(1426, "Dun Morogh", 0.104, 0.120),
        new(1427, "Searing Gorge", 0.112, 0.120),
        new(1428, "Burning Steppes", 0.135, 0.145),
        new(1429, "Elwynn Forest", 0.104, 0.120),
        new(1430, "Deadwind Pass", 0.072, 0.088),
        new(1431, "Duskwood", 0.128, 0.135),
        new(1432, "Loch Modan", 0.120, 0.128),
        new(1433, "Redridge Mountains", 0.120, 0.128),
        new(1434, "Stranglethorn Vale", 0.180, 0.190),
        new(1435, "Swamp of Sorrows", 0.135, 0.145),
        new(1436, "Westfall", 0.120, 0.128),
        new(1437, "Wetlands", 0.135, 0.145),
        new(1453, "Stormwind", 0.045, 0.045),
        new(1455, "Ironforge", 0.045, 0.045),
        new(1458, "Undercity", 0.045, 0.045),
    ];

    public static bool TryConvert(WowCharacterData character, out double mapX, out double mapY)
        => TryConvert(character.MapId, character.X, character.Y, out mapX, out mapY, character.Zone, character.SubZone);

    public static bool TryConvert(
        int mapId,
        double zoneX,
        double zoneY,
        out double mapX,
        out double mapY,
        string? zone = null,
        string? subZone = null)
    {
        mapX = 0;
        mapY = 0;

        NormalizeCoords(ref zoneX, ref zoneY);

        if (zoneX <= 0 && zoneY <= 0)
            return false;

        var effectiveMapId = ResolveEffectiveMapId(mapId, zone, subZone);
        if (!TryGetRect(effectiveMapId, zone, subZone, out var rect))
            return false;

        var point = ZoneToCarto(rect, zoneX, zoneY);
        mapX = Math.Clamp(point.X, 0, 1);
        mapY = Math.Clamp(point.Y, 0, 1);
        return true;
    }

    /// <summary>Normalise 0–100 vers 0–1 si besoin.</summary>
    public static void NormalizeCoords(ref double zoneX, ref double zoneY)
    {
        if (zoneX > 1.5) zoneX /= 100.0;
        if (zoneY > 1.5) zoneY /= 100.0;
    }

    public static CartoMapPoint ZoneToCarto(CartoMapRect rect, double zoneX, double zoneY)
    {
        var x = Math.Clamp(zoneX, 0, 1);
        var y = Math.Clamp(zoneY, 0, 1);
        // Carte zone WoW Classic : (0,0) en haut à gauche, X → droite, Y → bas (comme l'overlay carte in-game).
        return new CartoMapPoint(
            rect.Left + x * rect.Width,
            rect.Top + y * rect.Height);
    }

    public static CartoMapRect RectAt(double centerX, double centerY, double width, double height)
        => new(centerX - width / 2, centerY - height / 2, width, height);

    /// <summary>
    /// Calibre un rectangle zone à partir de deux repères (coords jeu 0–1 + position carto 0–1).
    /// </summary>
    public static CartoMapRect CalibrateFromTwoPoints(
        double x1, double y1, double cartoX1, double cartoY1,
        double x2, double y2, double cartoX2, double cartoY2)
    {
        var width = Math.Abs(x2 - x1) < 1e-6 ? 0.1 : (cartoX2 - cartoX1) / (x2 - x1);
        var height = Math.Abs(y2 - y1) < 1e-6 ? 0.1 : (cartoY2 - cartoY1) / (y2 - y1);
        var left = cartoX1 - x1 * width;
        var top = cartoY1 - y1 * height;
        return new CartoMapRect(left, top, width, height);
    }

    /// <summary>Le texte de zone in-game prime sur mapId (souvent continent ou parent incorrect).</summary>
    public static int ResolveEffectiveMapId(int mapId, string? zone, string? subZone)
    {
        // En capitale, l'UiMapID de l'addon (coords ville) prime sur le libellé de zone parent.
        if (mapId > 0 && CapitalMapIds.Contains(mapId))
            return mapId;

        // WowSync renvoie souvent 1414/1415 (tout le continent) : ne pas projeter avec le rectangle continent.
        if (IsContinentMap(mapId))
        {
            if (TryResolveMapIdFromAlias(subZone, out var aliasFromSub))
                return aliasFromSub;
            if (TryResolveMapIdFromAlias(zone, out var aliasFromZone))
                return aliasFromZone;
        }

        if (TryResolveMapIdFromZoneText(zone, subZone, out var fromText))
        {
            if (CapitalMapIds.Contains(fromText) && CapitalMapIds.Contains(mapId))
                return mapId;
            // Ex. zone « Orgrimmar » mais mapId Durotar (1411) : coords sur la carte parente.
            if (CapitalMapIds.Contains(fromText) && mapId > 0 && !IsContinentMap(mapId))
                return mapId;
            if (!IsContinentMap(fromText))
                return fromText;
        }

        if (mapId > 0 && !IsContinentMap(mapId))
            return mapId;

        if (TryResolveMapIdFromAlias(subZone, out var aliasId) ||
            TryResolveMapIdFromAlias(zone, out aliasId))
            return aliasId;

        return mapId;
    }

    public static bool TryGetRect(int mapId, string? zone, string? subZone, out CartoMapRect rect)
    {
        var effectiveId = ResolveEffectiveMapId(mapId, zone, subZone);

        // 1. Zone résolue par nom (Désolace, Silithus…)
        if (effectiveId > 0 && !IsContinentMap(effectiveId) && ByMapId.TryGetValue(effectiveId, out rect))
            return true;

        if (TryResolveRectByZoneName(zone, subZone, out rect))
            return true;

        // 2. Hub de vol → zone parente
        if (TryResolveParentZoneFromHub(subZone, out var parentMapId) ||
            TryResolveParentZoneFromHub(zone, out parentMapId))
        {
            if (ByMapId.TryGetValue(parentMapId, out rect))
                return true;
        }

        // 3. Capitale
        if (mapId > 0 && CityMapIds.Contains(mapId) && ByMapId.TryGetValue(mapId, out rect))
            return true;

        // 4. Continent (coords 0–1 sur Kalimdor / EK entier uniquement)
        if (mapId > 0 && ContinentRects.TryGetValue(mapId, out rect))
            return true;

        return false;
    }

    private static bool TryResolveMapIdFromZoneText(string? zone, string? subZone, out int mapId)
    {
        mapId = 0;
        var bestScore = 0;
        var bestMapId = 0;

        foreach (var candidate in ZoneLabelCandidates(zone, subZone))
        {
            var normalized = NormalizeZoneName(candidate);
            if (ContinentZoneLabels.Contains(normalized)) continue;

            if (ZoneNameToMapId.TryGetValue(normalized, out mapId))
                return true;

            if (TryResolveMapIdFromAlias(candidate, out mapId))
                return true;

            foreach (var entry in ByZoneName)
            {
                var score = ScoreZoneMatch(normalized, entry.NameEn, entry.NameFr);
                if (score <= bestScore) continue;
                if (!ZoneNameToMapId.TryGetValue(NormalizeZoneName(entry.NameEn), out var id))
                    continue;
                bestScore = score;
                bestMapId = id;
            }
        }

        if (bestScore < 4 || bestMapId == 0)
            return false;

        mapId = bestMapId;
        return true;
    }

    /// <summary>Ex. « Terres ingrates (Badlands) » → les deux libellés.</summary>
    private static IEnumerable<string> ZoneLabelCandidates(string? zone, string? subZone)
    {
        foreach (var raw in new[] { zone, subZone })
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            yield return raw;

            var open = raw.IndexOf('(');
            var close = raw.LastIndexOf(')');
            if (open >= 0 && close > open + 1)
                yield return raw[(open + 1)..close];
        }
    }

    private static bool TryResolveParentZoneFromHub(string? name, out int mapId)
    {
        mapId = 0;
        if (string.IsNullOrWhiteSpace(name)) return false;
        return HubToParentZoneMapId.TryGetValue(NormalizeZoneName(name), out mapId);
    }

    /// <summary>UiMapID pour les cartes terrain (capitales → zone parente).</summary>
    public static int ResolveTerrainZoneMapId(int mapId, string? zone, string? subZone)
    {
        var id = ResolveEffectiveMapId(mapId, zone, subZone);
        if (!CityMapIds.Contains(id))
            return id;

        if (TryResolveParentZoneFromHub(subZone, out var fromSub) ||
            TryResolveParentZoneFromHub(zone, out fromSub))
            return fromSub;

        return id switch
        {
            1455 => 1426, // Ironforge → Dun Morogh
            1454 => 1411, // Orgrimmar → Durotar
            1453 => 1429, // Stormwind → Elwynn
            1456 => 1412, // Thunder Bluff → Mulgore
            1457 => 1438, // Darnassus → Teldrassil
            1458 => 1420, // Undercity → Tirisfal
            _ => id
        };
    }

    public static bool IsContinentMap(int mapId) => ContinentRects.ContainsKey(mapId);

    private static bool TryResolveMapIdFromAlias(string? zoneName, out int mapId)
    {
        mapId = 0;
        if (string.IsNullOrWhiteSpace(zoneName)) return false;

        var key = NormalizeZoneName(zoneName);
        if (ZoneAliases.TryGetValue(key, out mapId))
            return true;

        foreach (var (alias, id) in ZoneAliases)
        {
            if (key.Length >= 4 && alias.Length >= 4 &&
                (key.Contains(alias, StringComparison.Ordinal) || alias.Contains(key, StringComparison.Ordinal)))
            {
                mapId = id;
                return true;
            }
        }

        return false;
    }

    public static bool TryResolveRectByZoneName(string? zone, string? subZone, out CartoMapRect rect)
    {
        rect = default;
        (string NameEn, string NameFr, CartoMapRect Rect)? best = null;
        var bestScore = 0;

        foreach (var candidate in new[] { subZone, zone })
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            var normalized = NormalizeZoneName(candidate);
            if (ContinentZoneLabels.Contains(normalized)) continue;

            foreach (var entry in ByZoneName)
            {
                var score = ScoreZoneMatch(normalized, entry.NameEn, entry.NameFr);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = entry;
                }
            }
        }

        if (bestScore < 4 || best == null)
            return false;

        rect = best.Value.Rect;
        return true;
    }

    private static int ScoreZoneMatch(string query, string nameEn, string nameFr)
    {
        var en = NormalizeZoneName(nameEn);
        var fr = NormalizeZoneName(nameFr);
        if (query == en || query == fr) return 100;
        if (en.Contains(query, StringComparison.Ordinal) || fr.Contains(query, StringComparison.Ordinal)) return 50 + query.Length;
        if (query.Contains(en, StringComparison.Ordinal) || query.Contains(fr, StringComparison.Ordinal)) return 40 + en.Length;
        return 0;
    }

    public static string? GetZoneLabel(int mapId)
    {
        var entry = ZoneEntries.FirstOrDefault(e => e.MapId == mapId);
        return entry.MapId == mapId ? entry.ZoneNameEn : null;
    }

    public readonly record struct ZoneCatalogEntry(int MapId, string NameEn, string NameFr, string DisplayName);

    public static IReadOnlyList<ZoneCatalogEntry> GetZoneCatalog()
    {
        return ZoneEntries
            .Select(entry =>
            {
                var zone = MapOverlayData.Zones.FirstOrDefault(z =>
                    z.NameEN.Equals(entry.ZoneNameEn, StringComparison.OrdinalIgnoreCase));
                var nameFr = zone?.NameFR ?? entry.ZoneNameEn;
                var displayName = CapitalMapIds.Contains(entry.MapId) ? entry.ZoneNameEn : nameFr;
                return new ZoneCatalogEntry(entry.MapId, entry.ZoneNameEn, nameFr, displayName);
            })
            .OrderBy(z => z.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static bool TryGetMapRect(int mapId, out CartoMapRect rect)
        => ByMapId.TryGetValue(mapId, out rect);

    public static CartoMapRect CreateDefaultRect(int mapId)
    {
        if (CapitalMapIds.Contains(mapId))
            return new CartoMapRect(0.05, 0.05, 0.90, 0.90);

        var zone = GetOverlayZoneForMapId(mapId);
        if (zone != null)
        {
            return CapitalMapIds.Contains(mapId)
                ? RectAt(zone.X, zone.Y, 0.058, 0.072)
                : RectAt(zone.X, zone.Y, 0.085, 0.090);
        }

        return new CartoMapRect(0.40, 0.40, 0.085, 0.090);
    }

    /// <summary>Minimum pendant l'édition (réglage fin, pas d'agrandissement forcé).</summary>
    public static (double MinWidth, double MinHeight) GetEditorMinimumZoneSize(int mapId)
        => CapitalMapIds.Contains(mapId) ? (0.02, 0.02) : (0.004, 0.004);

    public static (double MinWidth, double MinHeight) GetMinimumZoneSize(int mapId)
        => GetEditorMinimumZoneSize(mapId);

    /// <summary>Garde des valeurs valides sans imposer une taille minimale large.</summary>
    public static CartoMapRect SanitizeZoneRect(CartoMapRect rect)
    {
        const double eps = 0.001;
        var w = Math.Clamp(rect.Width, eps, 1);
        var h = Math.Clamp(rect.Height, eps, 1);
        var left = Math.Clamp(rect.Left, 0, Math.Max(0, 1 - w));
        var top = Math.Clamp(rect.Top, 0, Math.Max(0, 1 - h));
        return new CartoMapRect(left, top, w, h);
    }

    public static Dictionary<int, CartoMapRect> SanitizeZoneRects(
        IReadOnlyDictionary<int, CartoMapRect> userRects)
    {
        var result = new Dictionary<int, CartoMapRect>();
        foreach (var (mapId, rect) in userRects)
            result[mapId] = SanitizeZoneRect(rect);
        return result;
    }

    public static void ApplyUserRects(IReadOnlyDictionary<int, CartoMapRect> userRects)
    {
        foreach (var (mapId, rect) in userRects)
        {
            if (IsContinentMap(mapId)) continue;
            ByMapId[mapId] = SanitizeZoneRect(rect);
        }

        RebuildByZoneNameRects();
    }

    private static MapZone? GetOverlayZoneForMapId(int mapId)
    {
        var entry = ZoneEntries.FirstOrDefault(e => e.MapId == mapId);
        if (entry.MapId != mapId) return null;
        return MapOverlayData.Zones.FirstOrDefault(z =>
            z.NameEN.Equals(entry.ZoneNameEn, StringComparison.OrdinalIgnoreCase));
    }

    public static bool TryGetCatalogEntry(int mapId, out ZoneCatalogEntry entry)
    {
        foreach (var item in GetZoneCatalog())
        {
            if (item.MapId != mapId) continue;
            entry = item;
            return true;
        }

        entry = default;
        return false;
    }

    private static string NormalizeZoneName(string name)
    {
        var s = name.Trim();
        var paren = s.IndexOf('(');
        if (paren > 0)
            s = s[..paren].Trim();
        return s
            .Replace("'", "'", StringComparison.Ordinal)
            .Replace("'", "'", StringComparison.Ordinal)
            .Replace("'", "", StringComparison.Ordinal)
            .Replace("é", "e", StringComparison.OrdinalIgnoreCase)
            .Replace("è", "e", StringComparison.OrdinalIgnoreCase)
            .Replace("ê", "e", StringComparison.OrdinalIgnoreCase)
            .Replace("à", "a", StringComparison.OrdinalIgnoreCase)
            .Replace("ô", "o", StringComparison.OrdinalIgnoreCase)
            .Replace("û", "u", StringComparison.OrdinalIgnoreCase)
            .Replace("î", "i", StringComparison.OrdinalIgnoreCase)
            .Replace("ç", "c", StringComparison.OrdinalIgnoreCase)
            .Replace("â", "a", StringComparison.OrdinalIgnoreCase)
            .Replace("ù", "u", StringComparison.OrdinalIgnoreCase)
            .Replace("  ", " ", StringComparison.Ordinal);
    }
}
