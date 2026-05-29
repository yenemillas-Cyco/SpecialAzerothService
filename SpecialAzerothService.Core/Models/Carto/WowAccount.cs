namespace SpecialAzerothService.Core.Models.Carto;

public sealed class WowAccount
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Nom du dossier WTF WowSync (clé unique, ne pas fusionner avec le nom affiché).</summary>
    public string SourceFolder { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public bool IsHidden { get; set; }
    public override string ToString() => Name;
}
