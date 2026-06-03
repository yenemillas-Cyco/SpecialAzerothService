using System.Collections.ObjectModel;
using SpecialAzerothService.Core.Models.Carto;

namespace WindowsOrganiserApp.Models.Carto;

public enum CartoRosterNodeKind
{
    User,
    Account,
    Category,
    Character
}

/// <summary>Nœud TreeView roster : utilisateur → compte WTF → catégorie (persos dans le cadre).</summary>
public sealed class CartoRosterTreeNode
{
    public CartoRosterNodeKind Kind { get; init; }
    public string Title { get; init; } = "";
    public ObservableCollection<CartoRosterTreeNode> Children { get; } = [];

    public CartoUser? User { get; init; }
    public WowAccount? Account { get; init; }
    public CharacterStatus? Category { get; init; }
    public WowCharacter? Character { get; init; }

    /// <summary>Personnages affichés dans le cadre catégorie (pas des nœuds TreeView séparés).</summary>
    public List<WowCharacter> CategoryCharacters { get; } = [];

    public long GoldCopper { get; init; }
    public int CharacterCount { get; init; }

    public int Depth { get; set; }

    public string? ExpandKey { get; init; }

    public bool IsExpanded { get; set; } = true;
}
