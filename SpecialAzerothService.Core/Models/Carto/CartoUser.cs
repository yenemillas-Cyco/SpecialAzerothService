namespace SpecialAzerothService.Core.Models.Carto;

/// <summary>Utilisateur Carto — regroupe un ou plusieurs dossiers WTF (comptes WoW).</summary>
public sealed class CartoUser
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string Name { get; set; } = "";

    public int SortOrder { get; set; }

    /// <summary>Masque les marqueurs carte de cet utilisateur (le roster reste affiché).</summary>
    public bool IsRosterSubtreeHidden { get; set; }
}
