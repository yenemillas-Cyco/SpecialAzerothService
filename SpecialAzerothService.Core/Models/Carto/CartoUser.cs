namespace SpecialAzerothService.Core.Models.Carto;

/// <summary>Utilisateur Carto — regroupe un ou plusieurs dossiers WTF (comptes WoW).</summary>
public sealed class CartoUser
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string Name { get; set; } = "";

    public int SortOrder { get; set; }

    /// <summary>Masque tout le sous-arbre roster + les persos de cet utilisateur sur la carte.</summary>
    public bool IsRosterSubtreeHidden { get; set; }
}
