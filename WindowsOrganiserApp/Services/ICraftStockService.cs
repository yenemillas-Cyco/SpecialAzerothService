using WindowsOrganiserApp.Models.Craft;

namespace WindowsOrganiserApp.Services;

public interface ICraftStockService
{
    /// <summary>Propriétaires Carto (utilisateurs regroupant les comptes WTF).</summary>
    IReadOnlyList<CraftStockOwnerInfo> GetAvailableOwners();

    /// <summary>Stock inventaire/banque pour les propriétaires sélectionnés.</summary>
    CraftStockSnapshot ReadStockForOwners(IReadOnlyCollection<string> userIds);
}
