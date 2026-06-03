namespace SpecialAzerothService.Core.Models.Carto;

public enum AccountScope
{
    /// <summary>Mon compte — tes dossiers WTF.</summary>
    Mine,

    /// <summary>Obsolète — compte d'un autre utilisateur local.</summary>
    Friend
}

/// <summary>Réglage d'un dossier WTF (clé = nom du dossier Account).</summary>
public sealed class CartoAccountConfig
{
    public string DisplayName { get; set; } = "";

    /// <summary>Utilisateur propriétaire de ce dossier WTF.</summary>
    public string? UserId { get; set; }

    /// <summary>Obsolète — migré vers <see cref="UserId"/>.</summary>
    public AccountScope Scope { get; set; } = AccountScope.Mine;

    /// <summary>Obsolète — migré vers utilisateur (ex. Harry → Eloi).</summary>
    public string? FriendLabel { get; set; }

    /// <summary>Obsolète — migré vers <see cref="WowCharacter.IsHidden"/> par perso.</summary>
    public bool IsHiddenOnMap { get; set; }
}
