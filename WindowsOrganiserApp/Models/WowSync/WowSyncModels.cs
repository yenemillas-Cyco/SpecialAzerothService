namespace WindowsOrganiserApp.Models.WowSync;

public sealed class WowAccountData
{
    public string AccountName { get; set; } = "";
    public List<WowCharacterData> Characters { get; set; } = [];
}

public sealed class WowCharacterData
{
    public string Name { get; set; } = "";
    public string Realm { get; set; } = "";
    public int Level { get; set; }
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

    public string GoldDisplay
    {
        get
        {
            var g = Gold / 10000;
            var s = (Gold % 10000) / 100;
            var c = Gold % 100;
            return $"{g}g {s}s {c}c";
        }
    }

    public string PositionDisplay => X > 0 || Y > 0 ? $"{X * 100:F1}, {Y * 100:F1}" : "";
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
    public long Icon { get; set; }
    public int Quality { get; set; }
    public string Display => Count > 1 ? $"{Name} x{Count}" : Name;

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
