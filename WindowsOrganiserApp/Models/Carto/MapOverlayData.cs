namespace WindowsOrganiserApp.Models.Carto;

public enum Faction { Alliance, Horde, Neutral }

public sealed class MapZone
{
    public string NameEN { get; init; } = "";
    public string NameFR { get; init; } = "";
    public double X { get; init; }
    public double Y { get; init; }
    public int LevelMin { get; init; }
    public int LevelMax { get; init; }
    public bool IsCapital { get; init; }
    public Faction? CapitalFaction { get; init; }
}

public sealed class FlightNode
{
    public string NameEN { get; init; } = "";
    public string NameFR { get; init; } = "";
    public Faction Faction { get; init; }
    public double X { get; init; }
    public double Y { get; init; }
}

public sealed class FlightRoute
{
    public int FromIndex { get; init; }
    public int ToIndex { get; init; }
}

public static class MapOverlayData
{
    public static readonly MapZone[] Zones =
    [
        // === CAPITALES ===
        new() { NameEN = "Darnassus", NameFR = "Darnassus", X = 0.080, Y = 0.045, IsCapital = true, CapitalFaction = Faction.Alliance, LevelMin = 0, LevelMax = 0 },
        new() { NameEN = "Orgrimmar", NameFR = "Orgrimmar", X = 0.305, Y = 0.250, IsCapital = true, CapitalFaction = Faction.Horde, LevelMin = 0, LevelMax = 0 },
        new() { NameEN = "Thunder Bluff", NameFR = "Pitons du Tonnerre", X = 0.155, Y = 0.380, IsCapital = true, CapitalFaction = Faction.Horde, LevelMin = 0, LevelMax = 0 },
        new() { NameEN = "Undercity", NameFR = "Fossoyeuse", X = 0.640, Y = 0.095, IsCapital = true, CapitalFaction = Faction.Horde, LevelMin = 0, LevelMax = 0 },
        new() { NameEN = "Ironforge", NameFR = "Forgefer", X = 0.680, Y = 0.310, IsCapital = true, CapitalFaction = Faction.Alliance, LevelMin = 0, LevelMax = 0 },
        new() { NameEN = "Stormwind", NameFR = "Hurlevent", X = 0.650, Y = 0.460, IsCapital = true, CapitalFaction = Faction.Alliance, LevelMin = 0, LevelMax = 0 },

        // === KALIMDOR (X: 0.03 — 0.40) ===
        new() { NameEN = "Teldrassil", NameFR = "Teldrassil", X = 0.075, Y = 0.055, LevelMin = 1, LevelMax = 10 },
        new() { NameEN = "Darkshore", NameFR = "Sombrivage", X = 0.090, Y = 0.155, LevelMin = 10, LevelMax = 20 },
        new() { NameEN = "Moonglade", NameFR = "Reflet-de-Lune", X = 0.185, Y = 0.105, LevelMin = 55, LevelMax = 60 },
        new() { NameEN = "Winterspring", NameFR = "Berceau-de-l'Hiver", X = 0.250, Y = 0.095, LevelMin = 55, LevelMax = 60 },
        new() { NameEN = "Felwood", NameFR = "Gangrebois", X = 0.150, Y = 0.165, LevelMin = 48, LevelMax = 55 },
        new() { NameEN = "Azshara", NameFR = "Azshara", X = 0.295, Y = 0.195, LevelMin = 48, LevelMax = 55 },
        new() { NameEN = "Ashenvale", NameFR = "Orneval", X = 0.160, Y = 0.235, LevelMin = 18, LevelMax = 30 },
        new() { NameEN = "Stonetalon Mountains", NameFR = "Serres-Rocheuses", X = 0.110, Y = 0.295, LevelMin = 15, LevelMax = 27 },
        new() { NameEN = "The Barrens", NameFR = "Les Tarides", X = 0.225, Y = 0.330, LevelMin = 10, LevelMax = 25 },
        new() { NameEN = "Durotar", NameFR = "Durotar", X = 0.320, Y = 0.275, LevelMin = 1, LevelMax = 10 },
        new() { NameEN = "Mulgore", NameFR = "Mulgore", X = 0.150, Y = 0.370, LevelMin = 1, LevelMax = 10 },
        new() { NameEN = "Dustwallow Marsh", NameFR = "Marécage d'Âprefange", X = 0.290, Y = 0.400, LevelMin = 35, LevelMax = 45 },
        new() { NameEN = "Desolace", NameFR = "Désolace", X = 0.070, Y = 0.380, LevelMin = 30, LevelMax = 40 },
        new() { NameEN = "Thousand Needles", NameFR = "Mille Pointes", X = 0.210, Y = 0.450, LevelMin = 25, LevelMax = 35 },
        new() { NameEN = "Feralas", NameFR = "Féralas", X = 0.075, Y = 0.490, LevelMin = 40, LevelMax = 50 },
        new() { NameEN = "Tanaris", NameFR = "Tanaris", X = 0.235, Y = 0.555, LevelMin = 40, LevelMax = 50 },
        new() { NameEN = "Un'Goro Crater", NameFR = "Cratère d'Un'Goro", X = 0.165, Y = 0.540, LevelMin = 48, LevelMax = 55 },
        new() { NameEN = "Silithus", NameFR = "Silithus", X = 0.095, Y = 0.590, LevelMin = 55, LevelMax = 60 },

        // === EASTERN KINGDOMS (X: 0.55 — 0.92) ===
        new() { NameEN = "Tirisfal Glades", NameFR = "Clairières de Tirisfal", X = 0.625, Y = 0.060, LevelMin = 1, LevelMax = 10 },
        new() { NameEN = "Western Plaguelands", NameFR = "Maleterres de l'Ouest", X = 0.680, Y = 0.090, LevelMin = 51, LevelMax = 58 },
        new() { NameEN = "Eastern Plaguelands", NameFR = "Maleterres de l'Est", X = 0.755, Y = 0.075, LevelMin = 53, LevelMax = 60 },
        new() { NameEN = "Silverpine Forest", NameFR = "Forêt des Pins Argentés", X = 0.600, Y = 0.135, LevelMin = 10, LevelMax = 20 },
        new() { NameEN = "The Hinterlands", NameFR = "Les Hinterlands", X = 0.770, Y = 0.155, LevelMin = 40, LevelMax = 50 },
        new() { NameEN = "Alterac Mountains", NameFR = "Montagnes d'Alterac", X = 0.655, Y = 0.165, LevelMin = 30, LevelMax = 40 },
        new() { NameEN = "Hillsbrad Foothills", NameFR = "Contreforts de Hillsbrad", X = 0.665, Y = 0.195, LevelMin = 20, LevelMax = 30 },
        new() { NameEN = "Arathi Highlands", NameFR = "Hautes-terres d'Arathi", X = 0.755, Y = 0.200, LevelMin = 30, LevelMax = 40 },
        new() { NameEN = "Wetlands", NameFR = "Les Paluns", X = 0.730, Y = 0.260, LevelMin = 20, LevelMax = 30 },
        new() { NameEN = "Dun Morogh", NameFR = "Dun Morogh", X = 0.670, Y = 0.325, LevelMin = 1, LevelMax = 10 },
        new() { NameEN = "Loch Modan", NameFR = "Loch Modan", X = 0.755, Y = 0.305, LevelMin = 10, LevelMax = 20 },
        new() { NameEN = "Searing Gorge", NameFR = "Gorge des Vents Brûlants", X = 0.695, Y = 0.365, LevelMin = 43, LevelMax = 50 },
        new() { NameEN = "Burning Steppes", NameFR = "Steppes Ardentes", X = 0.710, Y = 0.400, LevelMin = 50, LevelMax = 58 },
        new() { NameEN = "Badlands", NameFR = "Terres Ingrates", X = 0.775, Y = 0.360, LevelMin = 35, LevelMax = 45 },
        new() { NameEN = "Redridge Mountains", NameFR = "Les Carmines", X = 0.760, Y = 0.440, LevelMin = 15, LevelMax = 25 },
        new() { NameEN = "Elwynn Forest", NameFR = "Forêt d'Elwynn", X = 0.660, Y = 0.475, LevelMin = 1, LevelMax = 10 },
        new() { NameEN = "Westfall", NameFR = "Marche de l'Ouest", X = 0.620, Y = 0.520, LevelMin = 10, LevelMax = 20 },
        new() { NameEN = "Duskwood", NameFR = "Bois de la Pénombre", X = 0.710, Y = 0.505, LevelMin = 18, LevelMax = 30 },
        new() { NameEN = "Deadwind Pass", NameFR = "Défilé de Deuillevent", X = 0.760, Y = 0.490, LevelMin = 55, LevelMax = 60 },
        new() { NameEN = "Swamp of Sorrows", NameFR = "Marais des Chagrins", X = 0.820, Y = 0.470, LevelMin = 35, LevelMax = 45 },
        new() { NameEN = "Blasted Lands", NameFR = "Terres Foudroyées", X = 0.810, Y = 0.535, LevelMin = 45, LevelMax = 55 },
        new() { NameEN = "Stranglethorn Vale", NameFR = "Vallée de Strangleronce", X = 0.670, Y = 0.615, LevelMin = 30, LevelMax = 45 },
    ];

    public static readonly FlightNode[] FlightNodes =
    [
        // === KALIMDOR — ALLIANCE ===
        /*  0 */ new() { NameEN = "Rut'theran Village", NameFR = "Village de Rut'theran", Faction = Faction.Alliance, X = 0.080, Y = 0.075 },
        /*  1 */ new() { NameEN = "Auberdine", NameFR = "Auberdine", Faction = Faction.Alliance, X = 0.095, Y = 0.170 },
        /*  2 */ new() { NameEN = "Astranaar", NameFR = "Astranaar", Faction = Faction.Alliance, X = 0.155, Y = 0.245 },
        /*  3 */ new() { NameEN = "Stonetalon Peak", NameFR = "Pic des Serres-Rocheuses", Faction = Faction.Alliance, X = 0.105, Y = 0.280 },
        /*  4 */ new() { NameEN = "Nijel's Point", NameFR = "Pointe de Nijel", Faction = Faction.Alliance, X = 0.080, Y = 0.365 },
        /*  5 */ new() { NameEN = "Thalanaar", NameFR = "Thalanaar", Faction = Faction.Alliance, X = 0.200, Y = 0.440 },
        /*  6 */ new() { NameEN = "Feathermoon Stronghold", NameFR = "Bastion de Pennelune", Faction = Faction.Alliance, X = 0.060, Y = 0.500 },
        /*  7 */ new() { NameEN = "Theramore Isle", NameFR = "Île de Theramore", Faction = Faction.Alliance, X = 0.310, Y = 0.410 },
        /*  8 */ new() { NameEN = "Gadgetzan", NameFR = "Gadgetzan", Faction = Faction.Neutral, X = 0.245, Y = 0.560 },
        /*  9 */ new() { NameEN = "Marshal's Refuge", NameFR = "Refuge du Maréchal", Faction = Faction.Neutral, X = 0.175, Y = 0.545 },
        /* 10 */ new() { NameEN = "Cenarion Hold", NameFR = "Fort Cénarien", Faction = Faction.Neutral, X = 0.100, Y = 0.595 },
        /* 11 */ new() { NameEN = "Everlook", NameFR = "Long-Guet", Faction = Faction.Neutral, X = 0.260, Y = 0.095 },
        /* 12 */ new() { NameEN = "Nighthaven", NameFR = "Havrenuit", Faction = Faction.Neutral, X = 0.195, Y = 0.105 },

        // === KALIMDOR — HORDE ===
        /* 13 */ new() { NameEN = "Orgrimmar", NameFR = "Orgrimmar", Faction = Faction.Horde, X = 0.305, Y = 0.255 },
        /* 14 */ new() { NameEN = "Crossroads", NameFR = "La Croisée", Faction = Faction.Horde, X = 0.235, Y = 0.320 },
        /* 15 */ new() { NameEN = "Thunder Bluff", NameFR = "Les Pitons du Tonnerre", Faction = Faction.Horde, X = 0.155, Y = 0.385 },
        /* 16 */ new() { NameEN = "Camp Taurajo", NameFR = "Camp Taurajo", Faction = Faction.Horde, X = 0.205, Y = 0.395 },
        /* 17 */ new() { NameEN = "Sun Rock Retreat", NameFR = "Retraite de Roche-Soleil", Faction = Faction.Horde, X = 0.120, Y = 0.300 },
        /* 18 */ new() { NameEN = "Splintertree Post", NameFR = "Poste de Bois-Fendu", Faction = Faction.Horde, X = 0.200, Y = 0.240 },
        /* 19 */ new() { NameEN = "Zoram'gar Outpost", NameFR = "Avant-poste de Zoram'gar", Faction = Faction.Horde, X = 0.085, Y = 0.235 },
        /* 20 */ new() { NameEN = "Freewind Post", NameFR = "Poste de Librevent", Faction = Faction.Horde, X = 0.215, Y = 0.460 },
        /* 21 */ new() { NameEN = "Shadowprey Village", NameFR = "Village de Proie-de-l'Ombre", Faction = Faction.Horde, X = 0.060, Y = 0.420 },
        /* 22 */ new() { NameEN = "Camp Mojache", NameFR = "Camp Mojache", Faction = Faction.Horde, X = 0.100, Y = 0.510 },
        /* 23 */ new() { NameEN = "Brackenwall Village", NameFR = "Village de Branchecombe", Faction = Faction.Horde, X = 0.270, Y = 0.385 },
        /* 24 */ new() { NameEN = "Valormok", NameFR = "Valormok", Faction = Faction.Horde, X = 0.280, Y = 0.200 },

        // === EASTERN KINGDOMS — ALLIANCE ===
        /* 25 */ new() { NameEN = "Ironforge", NameFR = "Forgefer", Faction = Faction.Alliance, X = 0.685, Y = 0.315 },
        /* 26 */ new() { NameEN = "Stormwind", NameFR = "Hurlevent", Faction = Faction.Alliance, X = 0.650, Y = 0.465 },
        /* 27 */ new() { NameEN = "Menethil Harbor", NameFR = "Port de Menethil", Faction = Faction.Alliance, X = 0.710, Y = 0.260 },
        /* 28 */ new() { NameEN = "Thelsamar", NameFR = "Thelsamar", Faction = Faction.Alliance, X = 0.755, Y = 0.310 },
        /* 29 */ new() { NameEN = "Refuge Pointe", NameFR = "Halte de Refuge", Faction = Faction.Alliance, X = 0.750, Y = 0.205 },
        /* 30 */ new() { NameEN = "Southshore", NameFR = "Austrivage", Faction = Faction.Alliance, X = 0.680, Y = 0.200 },
        /* 31 */ new() { NameEN = "Aerie Peak", NameFR = "Pic de l'Aigle", Faction = Faction.Alliance, X = 0.775, Y = 0.160 },
        /* 32 */ new() { NameEN = "Chillwind Camp", NameFR = "Camp du Noroît", Faction = Faction.Alliance, X = 0.660, Y = 0.110 },
        /* 33 */ new() { NameEN = "Light's Hope Chapel", NameFR = "Chapelle de l'Espoir de Lumière", Faction = Faction.Neutral, X = 0.765, Y = 0.078 },
        /* 34 */ new() { NameEN = "Sentinel Hill", NameFR = "Colline des Sentinelles", Faction = Faction.Alliance, X = 0.630, Y = 0.520 },
        /* 35 */ new() { NameEN = "Lakeshire", NameFR = "Comté-du-Lac", Faction = Faction.Alliance, X = 0.765, Y = 0.445 },
        /* 36 */ new() { NameEN = "Darkshire", NameFR = "Sombre-Comté", Faction = Faction.Alliance, X = 0.720, Y = 0.510 },
        /* 37 */ new() { NameEN = "Nethergarde Keep", NameFR = "Rempart du Néant", Faction = Faction.Alliance, X = 0.820, Y = 0.535 },
        /* 38 */ new() { NameEN = "Booty Bay", NameFR = "Baie-du-Butin", Faction = Faction.Neutral, X = 0.680, Y = 0.700 },
        /* 39 */ new() { NameEN = "Rebel Camp", NameFR = "Camp des Rebelles", Faction = Faction.Alliance, X = 0.670, Y = 0.580 },
        /* 40 */ new() { NameEN = "Thorium Point", NameFR = "Pointe du Thorium", Faction = Faction.Neutral, X = 0.700, Y = 0.365 },
        /* 41 */ new() { NameEN = "Morgan's Vigil", NameFR = "Veille de Morgan", Faction = Faction.Alliance, X = 0.715, Y = 0.410 },

        // === EASTERN KINGDOMS — HORDE ===
        /* 42 */ new() { NameEN = "Undercity", NameFR = "Fossoyeuse", Faction = Faction.Horde, X = 0.645, Y = 0.098 },
        /* 43 */ new() { NameEN = "The Sepulcher", NameFR = "Le Sépulcre", Faction = Faction.Horde, X = 0.600, Y = 0.140 },
        /* 44 */ new() { NameEN = "Tarren Mill", NameFR = "Moulin-de-Tarren", Faction = Faction.Horde, X = 0.670, Y = 0.190 },
        /* 45 */ new() { NameEN = "Hammerfall", NameFR = "Chute-du-Marteau", Faction = Faction.Horde, X = 0.770, Y = 0.210 },
        /* 46 */ new() { NameEN = "Revantusk Village", NameFR = "Village des Vengebroches", Faction = Faction.Horde, X = 0.800, Y = 0.165 },
        /* 47 */ new() { NameEN = "Kargath", NameFR = "Kargath", Faction = Faction.Horde, X = 0.785, Y = 0.370 },
        /* 48 */ new() { NameEN = "Flame Crest", NameFR = "Crête-de-Flamme", Faction = Faction.Horde, X = 0.705, Y = 0.395 },
        /* 49 */ new() { NameEN = "Stonard", NameFR = "Pierrêche", Faction = Faction.Horde, X = 0.835, Y = 0.475 },
        /* 50 */ new() { NameEN = "Grom'gol", NameFR = "Grom'gol", Faction = Faction.Horde, X = 0.660, Y = 0.600 },
    ];

    public static readonly FlightRoute[] AllianceRoutes =
    [
        new() { FromIndex = 0, ToIndex = 1 },
        new() { FromIndex = 1, ToIndex = 2 },
        new() { FromIndex = 2, ToIndex = 3 },
        new() { FromIndex = 1, ToIndex = 12 },
        new() { FromIndex = 2, ToIndex = 7 },
        new() { FromIndex = 3, ToIndex = 4 },
        new() { FromIndex = 4, ToIndex = 6 },
        new() { FromIndex = 5, ToIndex = 8 },
        new() { FromIndex = 7, ToIndex = 8 },
        new() { FromIndex = 6, ToIndex = 8 },
        new() { FromIndex = 8, ToIndex = 9 },
        new() { FromIndex = 9, ToIndex = 10 },
        new() { FromIndex = 11, ToIndex = 12 },
        new() { FromIndex = 25, ToIndex = 27 },
        new() { FromIndex = 25, ToIndex = 28 },
        new() { FromIndex = 27, ToIndex = 29 },
        new() { FromIndex = 27, ToIndex = 30 },
        new() { FromIndex = 29, ToIndex = 31 },
        new() { FromIndex = 30, ToIndex = 32 },
        new() { FromIndex = 32, ToIndex = 33 },
        new() { FromIndex = 26, ToIndex = 34 },
        new() { FromIndex = 26, ToIndex = 35 },
        new() { FromIndex = 35, ToIndex = 36 },
        new() { FromIndex = 36, ToIndex = 37 },
        new() { FromIndex = 36, ToIndex = 39 },
        new() { FromIndex = 39, ToIndex = 38 },
        new() { FromIndex = 40, ToIndex = 41 },
        new() { FromIndex = 25, ToIndex = 40 },
    ];

    public static readonly FlightRoute[] HordeRoutes =
    [
        new() { FromIndex = 13, ToIndex = 14 },
        new() { FromIndex = 14, ToIndex = 15 },
        new() { FromIndex = 14, ToIndex = 16 },
        new() { FromIndex = 14, ToIndex = 17 },
        new() { FromIndex = 14, ToIndex = 18 },
        new() { FromIndex = 18, ToIndex = 19 },
        new() { FromIndex = 18, ToIndex = 24 },
        new() { FromIndex = 16, ToIndex = 20 },
        new() { FromIndex = 15, ToIndex = 21 },
        new() { FromIndex = 20, ToIndex = 8 },
        new() { FromIndex = 22, ToIndex = 8 },
        new() { FromIndex = 23, ToIndex = 14 },
        new() { FromIndex = 24, ToIndex = 11 },
        new() { FromIndex = 11, ToIndex = 12 },
        new() { FromIndex = 8, ToIndex = 9 },
        new() { FromIndex = 9, ToIndex = 10 },
        new() { FromIndex = 42, ToIndex = 43 },
        new() { FromIndex = 42, ToIndex = 44 },
        new() { FromIndex = 44, ToIndex = 45 },
        new() { FromIndex = 45, ToIndex = 46 },
        new() { FromIndex = 45, ToIndex = 47 },
        new() { FromIndex = 47, ToIndex = 48 },
        new() { FromIndex = 48, ToIndex = 40 },
        new() { FromIndex = 49, ToIndex = 50 },
        new() { FromIndex = 50, ToIndex = 38 },
        new() { FromIndex = 42, ToIndex = 33 },
    ];
}
