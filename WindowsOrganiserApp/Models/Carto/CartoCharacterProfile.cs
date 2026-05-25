namespace WindowsOrganiserApp.Models.Carto;

/// <summary>
/// Profil utilisateur par personnage WowSync (clé Nom-Royaume).
/// Section <c>characterProfiles</c> dans carto.json — séparée de <c>characterExtras</c>.
/// </summary>
public sealed class CartoCharacterProfile
{
    public string SyncKey { get; set; } = "";

    /// <summary>Catégorie Carto (Main, Banque, Reroll, TP Boy, Clic Boys, …).</summary>
    public CharacterStatus Category { get; set; } = CharacterStatus.Reroll;

    public string Note { get; set; } = "";
}
