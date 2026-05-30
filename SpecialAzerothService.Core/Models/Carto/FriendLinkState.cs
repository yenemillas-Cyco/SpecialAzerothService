namespace SpecialAzerothService.Core.Models.Carto;

public enum FriendLinkState
{
    None = 0,
    /// <summary>Vous avez ajouté l'autre ; en attente de réciprocité.</summary>
    PendingOutbound = 1,
    /// <summary>L'autre vous a ajouté ; ajoutez-le pour un partage complet.</summary>
    PendingInbound = 2,
    /// <summary>Les deux se sont ajoutés — partage ami actif.</summary>
    Mutual = 3
}
