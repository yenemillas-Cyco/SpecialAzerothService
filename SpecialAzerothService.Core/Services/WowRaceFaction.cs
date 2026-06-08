using SpecialAzerothService.Core.Models.Carto;

namespace SpecialAzerothService.Core.Services;

/// <summary>Déduit Alliance / Horde à partir du nom de race (export WowSync ou client FR).</summary>
public static class WowRaceFaction
{
    public static Faction? ResolveFaction(string? race)
    {
        if (string.IsNullOrWhiteSpace(race))
            return null;

        var r = race.Trim().ToLowerInvariant();

        if (r is "orc" or "tauren" or "troll"
            or "mort-vivant" or "mort vivant" or "undead" or "forsaken" or "scourge"
            or "réprouvé" or "reprouve")
            return Faction.Horde;

        if (r is "humain" or "human" or "nain" or "dwarf" or "gnome"
            or "elfe de la nuit" or "night elf" or "nightelf")
            return Faction.Alliance;

        if (r.Contains("orc") || r.Contains("tauren") || r.Contains("troll")
            || r.Contains("mort") || r.Contains("undead") || r.Contains("scourge")
            || r.Contains("réprouvé") || r.Contains("reprouve"))
            return Faction.Horde;

        if (r.Contains("humain") || r.Contains("human") || r.Contains("nain") || r.Contains("dwarf")
            || r.Contains("gnome") || r.Contains("elfe") || r.Contains("night"))
            return Faction.Alliance;

        return null;
    }
}
