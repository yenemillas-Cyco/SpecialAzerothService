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
        // === KALIMDOR ===
        new() { NameEN = "Teldrassil", NameFR = "Teldrassil", X = 0.07, Y = 0.08, LevelMin = 1, LevelMax = 10 },
        new() { NameEN = "Darkshore", NameFR = "Sombrivage", X = 0.10, Y = 0.25, LevelMin = 10, LevelMax = 20 },
        new() { NameEN = "Moonglade", NameFR = "Reflet-de-Lune", X = 0.16, Y = 0.13, LevelMin = 55, LevelMax = 60 },
        new() { NameEN = "Winterspring", NameFR = "Berceau-de-l'Hiver", X = 0.22, Y = 0.12, LevelMin = 55, LevelMax = 60 },
        new() { NameEN = "Felwood", NameFR = "Gangrebois", X = 0.15, Y = 0.22, LevelMin = 48, LevelMax = 55 },
        new() { NameEN = "Azshara", NameFR = "Azshara", X = 0.27, Y = 0.24, LevelMin = 48, LevelMax = 55 },
        new() { NameEN = "Ashenvale", NameFR = "Orneval", X = 0.16, Y = 0.32, LevelMin = 18, LevelMax = 30 },
        new() { NameEN = "Stonetalon Mountains", NameFR = "Serres-Rocheuses", X = 0.11, Y = 0.38, LevelMin = 15, LevelMax = 27 },
        new() { NameEN = "The Barrens", NameFR = "Les Tarides", X = 0.22, Y = 0.40, LevelMin = 10, LevelMax = 25 },
        new() { NameEN = "Durotar", NameFR = "Durotar", X = 0.30, Y = 0.34, LevelMin = 1, LevelMax = 10 },
        new() { NameEN = "Mulgore", NameFR = "Mulgore", X = 0.16, Y = 0.46, LevelMin = 1, LevelMax = 10 },
        new() { NameEN = "Dustwallow Marsh", NameFR = "Marécage d'Âprefange", X = 0.30, Y = 0.46, LevelMin = 35, LevelMax = 45 },
        new() { NameEN = "Desolace", NameFR = "Désolace", X = 0.09, Y = 0.47, LevelMin = 30, LevelMax = 40 },
        new() { NameEN = "Thousand Needles", NameFR = "Mille Pointes", X = 0.21, Y = 0.54, LevelMin = 25, LevelMax = 35 },
        new() { NameEN = "Feralas", NameFR = "Féralas", X = 0.10, Y = 0.58, LevelMin = 40, LevelMax = 50 },
        new() { NameEN = "Tanaris", NameFR = "Tanaris", X = 0.24, Y = 0.68, LevelMin = 40, LevelMax = 50 },
        new() { NameEN = "Un'Goro Crater", NameFR = "Cratère d'Un'Goro", X = 0.17, Y = 0.68, LevelMin = 48, LevelMax = 55 },
        new() { NameEN = "Silithus", NameFR = "Silithus", X = 0.09, Y = 0.72, LevelMin = 55, LevelMax = 60 },
        new() { NameEN = "Hyjal", NameFR = "Hyjal", X = 0.17, Y = 0.18, LevelMin = 60, LevelMax = 60 },

        // === EASTERN KINGDOMS ===
        new() { NameEN = "Tirisfal Glades", NameFR = "Clairières de Tirisfal", X = 0.64, Y = 0.10, LevelMin = 1, LevelMax = 10 },
        new() { NameEN = "Western Plaguelands", NameFR = "Maleterres de l'Ouest", X = 0.68, Y = 0.13, LevelMin = 51, LevelMax = 58 },
        new() { NameEN = "Eastern Plaguelands", NameFR = "Maleterres de l'Est", X = 0.76, Y = 0.10, LevelMin = 53, LevelMax = 60 },
        new() { NameEN = "Silverpine Forest", NameFR = "Forêt des Pins Argentés", X = 0.62, Y = 0.18, LevelMin = 10, LevelMax = 20 },
        new() { NameEN = "The Hinterlands", NameFR = "Les Hinterlands", X = 0.79, Y = 0.19, LevelMin = 40, LevelMax = 50 },
        new() { NameEN = "Alterac Mountains", NameFR = "Montagnes d'Alterac", X = 0.67, Y = 0.22, LevelMin = 30, LevelMax = 40 },
        new() { NameEN = "Hillsbrad Foothills", NameFR = "Contreforts de Hillsbrad", X = 0.67, Y = 0.26, LevelMin = 20, LevelMax = 30 },
        new() { NameEN = "Arathi Highlands", NameFR = "Hautes-terres d'Arathi", X = 0.77, Y = 0.25, LevelMin = 30, LevelMax = 40 },
        new() { NameEN = "Wetlands", NameFR = "Les Paluns", X = 0.77, Y = 0.32, LevelMin = 20, LevelMax = 30 },
        new() { NameEN = "Dun Morogh", NameFR = "Dun Morogh", X = 0.70, Y = 0.37, LevelMin = 1, LevelMax = 10 },
        new() { NameEN = "Loch Modan", NameFR = "Loch Modan", X = 0.78, Y = 0.37, LevelMin = 10, LevelMax = 20 },
        new() { NameEN = "Searing Gorge", NameFR = "Gorge des Vents Brûlants", X = 0.72, Y = 0.42, LevelMin = 43, LevelMax = 50 },
        new() { NameEN = "Burning Steppes", NameFR = "Steppes Ardentes", X = 0.72, Y = 0.46, LevelMin = 50, LevelMax = 58 },
        new() { NameEN = "Badlands", NameFR = "Terres Ingrates", X = 0.79, Y = 0.42, LevelMin = 35, LevelMax = 45 },
        new() { NameEN = "Redridge Mountains", NameFR = "Les Carmines", X = 0.80, Y = 0.50, LevelMin = 15, LevelMax = 25 },
        new() { NameEN = "Elwynn Forest", NameFR = "Forêt d'Elwynn", X = 0.74, Y = 0.54, LevelMin = 1, LevelMax = 10 },
        new() { NameEN = "Westfall", NameFR = "Marche de l'Ouest", X = 0.69, Y = 0.58, LevelMin = 10, LevelMax = 20 },
        new() { NameEN = "Duskwood", NameFR = "Bois de la Pénombre", X = 0.76, Y = 0.58, LevelMin = 18, LevelMax = 30 },
        new() { NameEN = "Deadwind Pass", NameFR = "Défilé de Deuillevent", X = 0.80, Y = 0.55, LevelMin = 55, LevelMax = 60 },
        new() { NameEN = "Swamp of Sorrows", NameFR = "Marais des Chagrins", X = 0.85, Y = 0.52, LevelMin = 35, LevelMax = 45 },
        new() { NameEN = "Blasted Lands", NameFR = "Terres Foudroyées", X = 0.84, Y = 0.58, LevelMin = 45, LevelMax = 55 },
        new() { NameEN = "Stranglethorn Vale", NameFR = "Vallée de Strangleronce", X = 0.73, Y = 0.68, LevelMin = 30, LevelMax = 45 },
    ];

    public static readonly FlightNode[] FlightNodes =
    [
        // === KALIMDOR — ALLIANCE ===
        /*  0 */ new() { NameEN = "Rut'theran Village", NameFR = "Village de Rut'theran", Faction = Faction.Alliance, X = 0.08, Y = 0.12 },
        /*  1 */ new() { NameEN = "Auberdine", NameFR = "Auberdine", Faction = Faction.Alliance, X = 0.10, Y = 0.22 },
        /*  2 */ new() { NameEN = "Astranaar", NameFR = "Astranaar", Faction = Faction.Alliance, X = 0.17, Y = 0.30 },
        /*  3 */ new() { NameEN = "Stonetalon Peak", NameFR = "Pic des Serres-Rocheuses", Faction = Faction.Alliance, X = 0.12, Y = 0.34 },
        /*  4 */ new() { NameEN = "Nijel's Point", NameFR = "Pointe de Nijel", Faction = Faction.Alliance, X = 0.09, Y = 0.44 },
        /*  5 */ new() { NameEN = "Thalanaar", NameFR = "Thalanaar", Faction = Faction.Alliance, X = 0.20, Y = 0.52 },
        /*  6 */ new() { NameEN = "Feathermoon Stronghold", NameFR = "Bastion de Pennelune", Faction = Faction.Alliance, X = 0.07, Y = 0.57 },
        /*  7 */ new() { NameEN = "Theramore Isle", NameFR = "Île de Theramore", Faction = Faction.Alliance, X = 0.32, Y = 0.47 },
        /*  8 */ new() { NameEN = "Gadgetzan", NameFR = "Gadgetzan", Faction = Faction.Neutral, X = 0.25, Y = 0.66 },
        /*  9 */ new() { NameEN = "Marshal's Refuge", NameFR = "Refuge du Maréchal", Faction = Faction.Neutral, X = 0.18, Y = 0.66 },
        /* 10 */ new() { NameEN = "Cenarion Hold", NameFR = "Fort Cénarien", Faction = Faction.Neutral, X = 0.10, Y = 0.70 },
        /* 11 */ new() { NameEN = "Everlook", NameFR = "Long-Guet", Faction = Faction.Neutral, X = 0.23, Y = 0.11 },
        /* 12 */ new() { NameEN = "Nighthaven", NameFR = "Havrenuit", Faction = Faction.Neutral, X = 0.17, Y = 0.14 },

        // === KALIMDOR — HORDE ===
        /* 13 */ new() { NameEN = "Orgrimmar", NameFR = "Orgrimmar", Faction = Faction.Horde, X = 0.30, Y = 0.32 },
        /* 14 */ new() { NameEN = "Crossroads", NameFR = "La Croisée", Faction = Faction.Horde, X = 0.24, Y = 0.38 },
        /* 15 */ new() { NameEN = "Thunder Bluff", NameFR = "Les Pitons du Tonnerre", Faction = Faction.Horde, X = 0.16, Y = 0.44 },
        /* 16 */ new() { NameEN = "Camp Taurajo", NameFR = "Camp Taurajo", Faction = Faction.Horde, X = 0.21, Y = 0.46 },
        /* 17 */ new() { NameEN = "Sun Rock Retreat", NameFR = "Retraite de Roche-Soleil", Faction = Faction.Horde, X = 0.13, Y = 0.36 },
        /* 18 */ new() { NameEN = "Splintertree Post", NameFR = "Poste de Bois-Fendu", Faction = Faction.Horde, X = 0.20, Y = 0.30 },
        /* 19 */ new() { NameEN = "Zoram'gar Outpost", NameFR = "Avant-poste de Zoram'gar", Faction = Faction.Horde, X = 0.10, Y = 0.30 },
        /* 20 */ new() { NameEN = "Freewind Post", NameFR = "Poste de Librevent", Faction = Faction.Horde, X = 0.22, Y = 0.55 },
        /* 21 */ new() { NameEN = "Shadowprey Village", NameFR = "Village de Proie-de-l'Ombre", Faction = Faction.Horde, X = 0.07, Y = 0.50 },
        /* 22 */ new() { NameEN = "Camp Mojache", NameFR = "Camp Mojache", Faction = Faction.Horde, X = 0.11, Y = 0.60 },
        /* 23 */ new() { NameEN = "Brackenwall Village", NameFR = "Village de Branchecombe", Faction = Faction.Horde, X = 0.28, Y = 0.44 },
        /* 24 */ new() { NameEN = "Valormok", NameFR = "Valormok", Faction = Faction.Horde, X = 0.26, Y = 0.24 },

        // === EASTERN KINGDOMS — ALLIANCE ===
        /* 25 */ new() { NameEN = "Ironforge", NameFR = "Forgefer", Faction = Faction.Alliance, X = 0.71, Y = 0.36 },
        /* 26 */ new() { NameEN = "Stormwind", NameFR = "Hurlevent", Faction = Faction.Alliance, X = 0.73, Y = 0.53 },
        /* 27 */ new() { NameEN = "Menethil Harbor", NameFR = "Port de Menethil", Faction = Faction.Alliance, X = 0.74, Y = 0.31 },
        /* 28 */ new() { NameEN = "Thelsamar", NameFR = "Thelsamar", Faction = Faction.Alliance, X = 0.78, Y = 0.36 },
        /* 29 */ new() { NameEN = "Refuge Pointe", NameFR = "Halte de Refuge", Faction = Faction.Alliance, X = 0.77, Y = 0.24 },
        /* 30 */ new() { NameEN = "Southshore", NameFR = "Austrivage", Faction = Faction.Alliance, X = 0.70, Y = 0.25 },
        /* 31 */ new() { NameEN = "Aerie Peak", NameFR = "Pic de l'Aigle", Faction = Faction.Alliance, X = 0.79, Y = 0.20 },
        /* 32 */ new() { NameEN = "Chillwind Camp", NameFR = "Camp du Noroît", Faction = Faction.Alliance, X = 0.67, Y = 0.15 },
        /* 33 */ new() { NameEN = "Light's Hope Chapel", NameFR = "Chapelle de l'Espoir de Lumière", Faction = Faction.Neutral, X = 0.78, Y = 0.11 },
        /* 34 */ new() { NameEN = "Sentinel Hill", NameFR = "Colline des Sentinelles", Faction = Faction.Alliance, X = 0.70, Y = 0.58 },
        /* 35 */ new() { NameEN = "Lakeshire", NameFR = "Comté-du-Lac", Faction = Faction.Alliance, X = 0.80, Y = 0.50 },
        /* 36 */ new() { NameEN = "Darkshire", NameFR = "Sombre-Comté", Faction = Faction.Alliance, X = 0.77, Y = 0.58 },
        /* 37 */ new() { NameEN = "Nethergarde Keep", NameFR = "Rempart du Néant", Faction = Faction.Alliance, X = 0.85, Y = 0.56 },
        /* 38 */ new() { NameEN = "Booty Bay", NameFR = "Baie-du-Butin", Faction = Faction.Neutral, X = 0.73, Y = 0.76 },
        /* 39 */ new() { NameEN = "Rebel Camp", NameFR = "Camp des Rebelles", Faction = Faction.Alliance, X = 0.72, Y = 0.63 },
        /* 40 */ new() { NameEN = "Thorium Point", NameFR = "Pointe du Thorium", Faction = Faction.Neutral, X = 0.73, Y = 0.42 },
        /* 41 */ new() { NameEN = "Morgan's Vigil", NameFR = "Veille de Morgan", Faction = Faction.Alliance, X = 0.74, Y = 0.46 },

        // === EASTERN KINGDOMS — HORDE ===
        /* 42 */ new() { NameEN = "Undercity", NameFR = "Fossoyeuse", Faction = Faction.Horde, X = 0.65, Y = 0.12 },
        /* 43 */ new() { NameEN = "The Sepulcher", NameFR = "Le Sépulcre", Faction = Faction.Horde, X = 0.62, Y = 0.18 },
        /* 44 */ new() { NameEN = "Tarren Mill", NameFR = "Moulin-de-Tarren", Faction = Faction.Horde, X = 0.69, Y = 0.23 },
        /* 45 */ new() { NameEN = "Hammerfall", NameFR = "Chute-du-Marteau", Faction = Faction.Horde, X = 0.79, Y = 0.26 },
        /* 46 */ new() { NameEN = "Revantusk Village", NameFR = "Village des Vengebroches", Faction = Faction.Horde, X = 0.82, Y = 0.20 },
        /* 47 */ new() { NameEN = "Kargath", NameFR = "Kargath", Faction = Faction.Horde, X = 0.80, Y = 0.42 },
        /* 48 */ new() { NameEN = "Flame Crest", NameFR = "Crête-de-Flamme", Faction = Faction.Horde, X = 0.72, Y = 0.44 },
        /* 49 */ new() { NameEN = "Stonard", NameFR = "Pierrêche", Faction = Faction.Horde, X = 0.86, Y = 0.53 },
        /* 50 */ new() { NameEN = "Grom'gol", NameFR = "Grom'gol", Faction = Faction.Horde, X = 0.71, Y = 0.67 },
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
