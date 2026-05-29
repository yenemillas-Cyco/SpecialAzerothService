namespace SpecialAzerothService.Core.Models.Carto;

public enum QuestItemType
{
    Tete_de_Rend,
    Tete_dOnyxia,
    Tete_de_Nefarian,
    Coeur_de_Hakkar
}

public sealed class QuestItemEntry
{
    public QuestItemType Type { get; set; }
    public bool HasItem { get; set; }
    public DateTime? PlannedTurnIn { get; set; }
    public string? Note { get; set; }
}
