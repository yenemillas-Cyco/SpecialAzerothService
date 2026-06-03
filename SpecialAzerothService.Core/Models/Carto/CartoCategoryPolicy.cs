namespace SpecialAzerothService.Core.Models.Carto;

/// <summary>Visibilité carte par utilisateur et catégorie.</summary>
public sealed class CartoCategoryPolicy
{
    public string UserId { get; set; } = "";

    public CharacterStatus Category { get; set; }

    /// <summary>Masque les marqueurs carte de cette catégorie (le roster reste affiché).</summary>
    public bool IsRosterSubtreeHidden { get; set; }
}
