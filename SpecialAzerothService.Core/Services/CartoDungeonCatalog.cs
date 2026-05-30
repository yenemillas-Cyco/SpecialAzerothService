namespace SpecialAzerothService.Core.Services;

/// <summary>Catalogue des repères carte monde : instances + lieux-dits.</summary>
public static class CartoDungeonCatalog
{
    public readonly record struct DungeonEntry(string Key, string NameFr, string ParentZoneFr, bool IsLieuDit = false);

    public static IReadOnlyList<DungeonEntry> All { get; } =
    [
        new("lights_hope_chapel", "Chapelle de l'Espoir de Lumière", "Maleterres de l'Est", IsLieuDit: true),
        new("ragefire", "Gouffre de Ragefeu", "Durotar / Orgrimmar"),
        new("deadmines", "Mortemines", "Marche de l'Ouest"),
        new("wailing_caverns", "Cavernes des Lamentations", "Les Tarides"),
        new("shadowfang_keep", "Donjon d'Ombrecroc", "Forêt des Pins argentés"),
        new("stockades", "La Prison", "Hurlevent"),
        new("blackfathom_deeps", "Profondeurs de Brassenoire", "Orneval"),
        new("razorfen_kraul", "Kraal de Tranchebauge", "Les Tarides"),
        new("razorfen_downs", "Souilles de Tranchebauge", "Les Tarides"),
        new("gnomeregan", "Gnomeregan", "Dun Morogh"),
        new("scarlet_monastery", "Monastère écarlate", "Clairières de Tirisfal"),
        new("uldaman", "Uldaman", "Terres ingrates"),
        new("zul_farrak", "Zul'Farrak", "Tanaris"),
        new("maraudon", "Maraudon", "Désolace"),
        new("sunken_temple", "Temple d'Atal'Hakkar", "Marais des Chagrins"),
        new("mont_blackrock", "Mont Blackrock", "Steppes Ardentes / BRD / Pic"),
        new("dire_maul", "Haches-Tripes", "Féralas"),
        new("stratholme", "Stratholme", "Maleterres de l'Est"),
        new("scholomance", "Scholomance", "Maleterres de l'Ouest"),
        new("zul_gurub", "Zul'Gurub", "Vallée de Strangleronce"),
        new("onyxia", "Repaire d'Onyxia", "Marécage d'Âprefange"),
        new("molten_core", "Cœur du Magma", "Mont Rochenoire"),
        new("gates_aq", "Portes d'Ahn'Qiraj", "Silithus"),
        new("ruins_aq", "Ruines d'Ahn'Qiraj", "Silithus"),
        new("temple_aq", "Temple d'Ahn'Qiraj", "Silithus"),
        new("blackwing_lair", "Repaire de l'Aile noire", "Mont Rochenoire"),
        new("naxxramas", "Naxxramas", "Maleterres de l'Est"),
    ];

    public static bool TryGet(string key, out DungeonEntry entry)
    {
        foreach (var d in All)
        {
            if (d.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                entry = d;
                return true;
            }
        }

        entry = default;
        return false;
    }
}
