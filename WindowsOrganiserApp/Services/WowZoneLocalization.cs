using WindowsOrganiserApp.Models.Carto;

namespace WindowsOrganiserApp.Services;

/// <summary>Affiche uniquement des noms de zone connus en français.</summary>
public static class WowZoneLocalization
{
    private static readonly Dictionary<string, string> Lookup = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> FrenchCanonical = new(StringComparer.OrdinalIgnoreCase);

    static WowZoneLocalization()
    {
        void Register(string? key, string french)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(french))
                return;

            var fr = french.Trim();
            FrenchCanonical.Add(fr);
            Lookup[key.Trim()] = fr;
            Lookup[Normalize(key)] = fr;
        }

        foreach (var z in MapOverlayData.Zones)
        {
            Register(z.NameEN, z.NameFR);
            Register(z.NameFR, z.NameFR);
            Register(ToSlug(z.NameEN), z.NameFR);
        }

        foreach (var n in MapOverlayData.FlightNodes)
        {
            Register(n.NameEN, n.NameFR);
            Register(n.NameFR, n.NameFR);
        }

        RegisterSubzones();
    }

    private static void RegisterSubzones()
    {
        void Add(string? key, string french)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(french))
                return;

            var fr = french.Trim();
            FrenchCanonical.Add(fr);
            Lookup[key.Trim()] = fr;
            Lookup[Normalize(key)] = fr;
        }

        Add("Crossroads", "La Croisée");
        Add("La Croisée", "La Croisée");
        Add("Croisée", "La Croisée");
        Add("Refuge Pointe", "Refuge de la Désolation");
        Add("Booty Bay", "Baie du Butin");
        Add("Gadgetzan", "Gadgetzan");
        Add("Auberdine", "Auberdine");
        Add("Ratchet", "Ratchet");
        Add("Splintertree Post", "Poste de Bois-brisé");
        Add("Freewind Post", "Poste de Librevent");
        Add("Camp Taurajo", "Camp Taurajo");
        Add("Theramore Isle", "Île de Theramore");
        Add("Feathermoon Stronghold", "Fort de Pennelune");
        Add("Nighthaven", "Havrenuit");
        Add("Everlook", "Long-guet");
        Add("Light's Hope Chapel", "Chapelle de l'Espoir de Lumière");
        Add("Chillwind Camp", "Camp du Noroît");
        Add("Thorium Point", "Halte du Thorium");
        Add("Kargath", "Kargath");
        Add("Nethergarde Keep", "Rempart-du-Néant");
        Add("Southshore", "Austrivage");
        Add("Tarren Mill", "Moulin-de-Tarren");
        Add("Hammerfall", "Trépas-d'Orgrim");
        Add("Grom'gol Base Camp", "Campement Grom'gol");
        Add("Stonard", "Pierrêche");
        Add("Flame Crest", "Corniche des Flammes");
        Add("Revantusk Village", "Village des Vengebroches");
        Add("Shadowprey Village", "Village de Cassecrête");
        Add("Sun Rock Retreat", "Retraite de Roche-Soleil");
        Add("Bloodvenom Post", "Poste de la Vénéneuse");
        Add("Emerald Sanctuary", "Sanctuaire d'émeraude");
        Add("Marshal's Refuge", "Refuge du Marshal");
        Add("Cenarion Hold", "Fort Cénarien");
        Add("Valormok", "Valormok");
        Add("Morgan's Vigil", "Veille de Morgan");
        Add("Darkshire", "Darkshire");
        Add("Raven Hill", "Colline-aux-Corbeaux");
        Add("Sentinel Hill", "Colline des Sentinelles");
        Add("Lakeshire", "Comté-du-Lac");
        Add("Menethil Harbor", "Port de Menethil");
        Add("The Sepulcher", "Le Sépulcre");
        Add("Undercity", "Fossoyeuse");
        Add("Ironforge", "Forgefer");
        Add("Stormwind City", "Hurlevent");
        Add("Orgrimmar", "Orgrimmar");
        Add("Valley of Strength", "Vallée de la Force");
        Add("Vallée de la Force", "Vallée de la Force");
        Add("Thunder Bluff", "Pitons du Tonnerre");
        Add("Darnassus", "Darnassus");
    }

    /// <summary>Retourne le nom FR ou vide si inconnu (jamais d'anglais).</summary>
    public static string ToFrenchOnly(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "";

        var trimmed = name.Trim();
        if (Lookup.TryGetValue(trimmed, out var fr))
            return fr;

        if (Lookup.TryGetValue(Normalize(trimmed), out fr))
            return fr;

        return "";
    }

    /// <summary>Une seule localisation française (sous-zone puis zone).</summary>
    public static string FormatDisplay(string? zone, string? subZone)
    {
        foreach (var candidate in new[] { subZone, zone })
        {
            var fr = ToFrenchOnly(candidate);
            if (!string.IsNullOrWhiteSpace(fr))
                return fr;
        }

        return "";
    }

    private static string ToSlug(string name) =>
        Normalize(name).Replace(' ', '_');

    private static string Normalize(string name)
        => name.Trim()
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
