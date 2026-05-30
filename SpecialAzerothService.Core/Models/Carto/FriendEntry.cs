using System.Text.Json.Serialization;

namespace SpecialAzerothService.Core.Models.Carto;

public sealed class FriendEntry
{
    public string Guid { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsVisible { get; set; } = true;
    public FriendLinkState LinkState { get; set; } = FriendLinkState.PendingOutbound;

    [JsonIgnore]
    public bool IsOnline { get; set; }

    [JsonIgnore]
    public bool IsMutual => LinkState == FriendLinkState.Mutual;

    [JsonIgnore]
    public string LinkStateDisplay => LinkState switch
    {
        FriendLinkState.Mutual => "Partage actif",
        FriendLinkState.PendingOutbound => "En attente (ajoutez-le aussi)",
        FriendLinkState.PendingInbound => "Vous a ajouté — répondez",
        _ => ""
    };
}
