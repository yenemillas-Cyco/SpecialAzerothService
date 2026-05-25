using System.Text.Json.Serialization;

namespace WindowsOrganiserApp.Models.Carto;

/// <summary>Données Carto conservées par personnage WowSync (clé Nom-Royaume).</summary>
public sealed class CartoCharacterExtras
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SyncKey { get; set; } = "";
    public string? AccountId { get; set; }

    /// <summary>Obsolète — migré vers <see cref="CartoData.CharacterProfiles"/>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWriting)]
    public CharacterStatus Status { get; set; } = CharacterStatus.Reroll;

    public List<ProfessionInfo> Professions { get; set; } = [];
    public List<CooldownEntry> Cooldowns { get; set; } = [];
    public List<QuestItemEntry> QuestItems { get; set; } = [];

    /// <summary>Obsolète — migré vers <see cref="CartoData.CharacterProfiles"/>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWriting)]
    public string Note { get; set; } = "";
    public int ShardCount { get; set; }
    public bool IsHidden { get; set; }
    public bool IsLocked { get; set; }
    public bool ExcludeFromSync { get; set; }

    /// <summary>Affiché sur la carte (sinon cadre Banque/Main/Reroll/TP à gauche).</summary>
    public bool IsPlacedOnMap { get; set; }

    /// <summary>Position manuelle sur la carte (sinon pile en haut à gauche).</summary>
    public bool HasCustomMapPosition { get; set; }
    public double MapX { get; set; }
    public double MapY { get; set; }
}
