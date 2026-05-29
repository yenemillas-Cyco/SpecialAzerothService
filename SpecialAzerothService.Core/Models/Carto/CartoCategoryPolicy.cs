namespace SpecialAzerothService.Core.Models.Carto;

/// <summary>Options de synchronisation par catégorie (Main, Banque, etc.) pour un utilisateur.</summary>
public sealed class CartoCategoryPolicy
{
    public string UserId { get; set; } = "";

    public CharacterStatus Category { get; set; }

    public bool SyncBank { get; set; } = true;
    public bool SyncInventory { get; set; } = true;
    public bool SyncProfessions { get; set; } = true;
    public bool SyncCooldowns { get; set; } = true;
    public bool SyncGold { get; set; } = true;
    public bool SyncSoulShards { get; set; } = true;

    /// <summary>Masque cette catégorie (sous-arbre + carte) pour l'utilisateur.</summary>
    public bool IsRosterSubtreeHidden { get; set; }
}
