namespace WindowsOrganiserApp.Models.Bounty;

public sealed class BountyData
{
    public string Rules { get; set; } = """
        -Le joueur doit être tué sur son main.
        -Un kill a la fois.
        -A tuer en donjon ou en raid.
        -Poster à la suite de ce post, la preuve de votre coup fatal.
        -Passer à la cible suivante.
        -25po ou 6 bijoux seront ajoutés à la prime si le joueur est tué sous WB.
        """;

    public List<BountyEntry> Bounties { get; set; } = [];
}
