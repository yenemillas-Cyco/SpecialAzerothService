namespace WindowsOrganiserApp.Models.Carto;

public enum CharacterStatus
{
    Main,
    Reroll,
    Banque,
    TpBoy,
    ClicBoys
}

public static class CharacterStatusExtensions
{
    public static string DisplayName(this CharacterStatus s) => s switch
    {
        CharacterStatus.Main => "Personnages",
        CharacterStatus.Reroll => "Personnages",
        CharacterStatus.Banque => "Banque",
        CharacterStatus.TpBoy => "TP Boy",
        CharacterStatus.ClicBoys => "Clic Boys",
        _ => s.ToString()
    };
}

public sealed class WowCharacter
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Clé WowSync : Nom-Royaume.</summary>
    public string SyncKey { get; set; } = "";

    public string Name { get; set; } = string.Empty;
    /// <summary>Race WoW (sync addon) — sert à l'icône de faction.</summary>
    public string Race { get; set; } = "";
    public WowClass Class { get; set; }
    public int Level { get; set; } = 1;
    public string? AccountId { get; set; }
    public CharacterStatus Status { get; set; } = CharacterStatus.Reroll;

    public double MapX { get; set; }
    public double MapY { get; set; }

    /// <summary>Affiché sur la carte (sinon dans le bandeau hors carte à droite).</summary>
    public bool IsPlacedOnMap { get; set; }

    /// <summary>True si l'utilisateur a déplacé le marqueur (persisté dans CharacterExtras).</summary>
    public bool HasCustomMapPosition { get; set; }

    /// <summary>Coords 0–1 sur la carte terrain de zone (si connues).</summary>
    public string? TerrainZoneSlug { get; set; }
    public double? TerrainZoneX { get; set; }
    public double? TerrainZoneY { get; set; }

    public List<ProfessionInfo> Professions { get; set; } = [];
    public List<CooldownEntry> Cooldowns { get; set; } = [];
    public List<QuestItemEntry> QuestItems { get; set; } = [];
    public string Note { get; set; } = string.Empty;
    public int ShardCount { get; set; }
    public bool IsHidden { get; set; }
    public bool IsLocked { get; set; }
    public bool ExcludeFromSync { get; set; }
    public bool IsExternal { get; set; }
    public string? ExternalSource { get; set; }
}
