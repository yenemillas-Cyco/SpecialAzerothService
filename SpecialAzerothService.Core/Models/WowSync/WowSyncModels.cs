using SpecialAzerothService.Core.Services;

using SpecialAzerothService.Core.Models.Carto;

namespace SpecialAzerothService.Core.Models.WowSync;

public sealed class WowAccountData
{
    /// <summary>Nom du dossier WTF (clé pour accountDisplayNames dans carto.json).</summary>
    public string SourceAccountName { get; set; } = "";

    public string AccountName { get; set; } = "";
    public List<WowCharacterData> Characters { get; set; } = [];

    public long TotalGoldCopper => Characters.Sum(c => c.Gold);
}

public sealed class WowCharacterData
{
    public string Name { get; set; } = "";
    public string Realm { get; set; } = "";
    public int Level { get; set; }
    /// <summary>Pourcentage XP vers prochain niveau (0–100), fourni par l'addon.</summary>
    public double XpPercent { get; set; } = -1;

    /// <summary>ID interne WoW (<see cref="UnitPVPRank"/>), 5–18 ou 0.</summary>
    public int PvpRankId { get; set; }

    /// <summary>Rang JcJ affiché (1–14), 0 si non classé.</summary>
    public int PvpRank { get; set; }

    /// <summary>Nom localisé du rang (ex. « Sergent »).</summary>
    public string PvpRankName { get; set; } = "";

    /// <summary>Progression vers le rang suivant (0–100), -1 si indisponible.</summary>
    public double PvpRankProgress { get; set; } = -1;

    /// <summary>Rang affiché (1–14), avec repli sur <see cref="PvpRankId"/> si besoin.</summary>
    public int DisplayPvpRank => PvpRank > 0 ? PvpRank : PvpRankId >= 5 ? PvpRankId - 4 : 0;

    public bool HasPvpRank => DisplayPvpRank > 0;

    /// <summary>Addon ≥ 1.6 — quêtes d'accès raids HL scannées.</summary>
    public bool HasRaidAttunementSync { get; set; }

    public bool AttunedMoltenCore { get; set; }
    public bool AttunedBlackwingLair { get; set; }
    public bool AttunedOnyxia { get; set; }
    public bool AttunedNaxxramas { get; set; }

    public bool IsRaidAttuned(RaidAttunementType type) => type switch
    {
        RaidAttunementType.MoltenCore => AttunedMoltenCore,
        RaidAttunementType.BlackwingLair => AttunedBlackwingLair,
        RaidAttunementType.Onyxia => AttunedOnyxia,
        RaidAttunementType.Naxxramas => AttunedNaxxramas,
        _ => false
    };

    public string Class { get; set; } = "";
    public string Race { get; set; } = "";
    public long Gold { get; set; }
    public string Zone { get; set; } = "";
    public string SubZone { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public int MapId { get; set; }
    public string LastUpdate { get; set; } = "";
    public List<WowProfession> Professions { get; set; } = [];
    public List<WowItem> Inventory { get; set; } = [];
    public List<WowItem> Bank { get; set; } = [];
    public List<WowMailEntry> Mail { get; set; } = [];
    public WowSyncMeta Sync { get; set; } = new();
    public List<WowProfessionCooldown> Cooldowns { get; set; } = [];

    /// <summary>CD métiers dont le personnage possède la recette / sort (addon WowSync).</summary>
    public List<string> KnownCooldownKeys { get; set; } = [];

    public string GoldDisplay
    {
        get
        {
            var g = Gold / 10000;
            var s = (Gold % 10000) / 100;
            var c = Gold % 100;
            return $"{g} po {s} pa {c} pc";
        }
    }

    public string ZoneDisplay => WowZoneLocalization.FormatDisplay(Zone, SubZone);

    public string PositionDisplay =>
        X > 0 || Y > 0
            ? $"{X * 100:F1}, {Y * 100:F1} — {ZoneDisplay}" + (MapId > 0 ? $" (map {MapId})" : "")
            : string.IsNullOrEmpty(ZoneDisplay) ? "Coords manquantes — redéployez l'addon et déconnectez-vous" : $"Coords manquantes — {ZoneDisplay}";
    /// <summary>Clé du tableau SavedVariables (peut différer de <see cref="Key"/> sur les accents).</summary>
    public string StorageKey { get; set; } = "";

    public string Key => $"{Name}-{Realm}";
}

public sealed class WowProfession
{
    public string Name { get; set; } = "";
    public int Rank { get; set; }
    public int MaxRank { get; set; }
    public string Display => $"{Name} {Rank}/{MaxRank}";
}

public sealed class WowItem
{
    public string Name { get; set; } = "";
    public int Count { get; set; } = 1;
    public int ItemId { get; set; }
    public int SpellId { get; set; }
    public long Icon { get; set; }
    public int Quality { get; set; }
    public string Display => Count > 1 ? $"{Name} x{Count}" : Name;

    public string QualityName => Quality switch
    {
        0 => "Médiocre",
        1 => "Commun",
        2 => "Peu commun",
        3 => "Rare",
        4 => "Épique",
        5 => "Légendaire",
        _ => ""
    };

    public string WowheadUrl => ItemId > 0
        ? $"https://www.wowhead.com/classic/fr/item={ItemId}"
        : "";

    public string QualityColor => Quality switch
    {
        0 => "#9D9D9D", // Poor (grey)
        1 => "#FFFFFF", // Common (white)
        2 => "#1EFF00", // Uncommon (green)
        3 => "#0070DD", // Rare (blue)
        4 => "#A335EE", // Epic (purple)
        5 => "#FF8000", // Legendary (orange)
        _ => "#E8D5A3"
    };

    public string QualityBorder => Quality switch
    {
        0 => "#309D9D9D",
        1 => "#30FFFFFF",
        2 => "#401EFF00",
        3 => "#400070DD",
        4 => "#40A335EE",
        5 => "#40FF8000",
        _ => "#30E8D5A3"
    };
}

public sealed class WowMailEntry
{
    public string Sender { get; set; } = "";
    public string Subject { get; set; } = "";
    public long Money { get; set; }
    public double DaysLeft { get; set; }
    public List<WowItem> Items { get; set; } = [];
}

public sealed class WowSyncMeta
{
    public string Inventory { get; set; } = "";
    public string Bank { get; set; } = "";
    public string Mail { get; set; } = "";
    public string Professions { get; set; } = "";
    public string Cooldowns { get; set; } = "";

    public bool HasInventory => !string.IsNullOrEmpty(Inventory);
    public bool HasBank => !string.IsNullOrEmpty(Bank);
    public bool HasMail => !string.IsNullOrEmpty(Mail);
    public bool HasProfessions => !string.IsNullOrEmpty(Professions);
    public bool HasCooldowns => !string.IsNullOrEmpty(Cooldowns);

    public string InventoryLabel => HasInventory ? $"✅ {Inventory}" : "— pas encore";
    public string BankLabel => HasBank ? $"✅ {Bank}" : "— ouvrir la banque";
    public string MailLabel => HasMail ? $"✅ {Mail}" : "— ouvrir le courrier";
    public string ProfessionsLabel => HasProfessions ? $"✅ {Professions}" : "— auto au login";
}

public sealed class WowProfessionCooldown
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public double RemainingSec { get; set; }
    public double ScannedAt { get; set; }

    public DateTime? ReadyAtUtc =>
        ScannedAt > 0 && RemainingSec > 0
            ? DateTimeOffset.FromUnixTimeSeconds((long)(ScannedAt + RemainingSec)).UtcDateTime
            : null;

    public bool WasExplicitlyScanned => ScannedAt > 0;

    public bool IsExplicitlyRunning => WasExplicitlyScanned && RemainingSec > 0;

    public bool IsExplicitlyReady => WasExplicitlyScanned && RemainingSec <= 0;

    public bool IsReady => ReadyAtUtc == null || DateTime.UtcNow >= ReadyAtUtc;

    public string Display
    {
        get
        {
            if (IsReady) return $"{ShortName} : prêt";
            var left = ReadyAtUtc!.Value - DateTime.UtcNow;
            if (left.TotalDays >= 1) return $"{ShortName} : {(int)left.TotalDays}j {left.Hours}h";
            if (left.TotalHours >= 1) return $"{ShortName} : {(int)left.TotalHours}h {left.Minutes}m";
            return $"{ShortName} : {Math.Max(0, (int)left.TotalMinutes)}m";
        }
    }

    private string ShortName => Key switch
    {
        "arcanite" => "Arcanite",
        "elemental" => "Transmu. élément.",
        "mooncloth" => "Étoffe lunaire",
        "salt" => "Tamis à sel",
        _ => string.IsNullOrEmpty(Name) ? Key : Name
    };
}
