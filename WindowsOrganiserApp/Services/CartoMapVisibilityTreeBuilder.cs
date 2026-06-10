using SpecialAzerothService.Core.Models.Carto;
using WindowsOrganiserApp.Models.Carto;
using WindowsOrganiserApp.ViewModels;

namespace WindowsOrganiserApp.Services;

/// <summary>Arbre propriétaire → compte → catégorie → personnage pour la gestion de visibilité carte.</summary>
public static class CartoMapVisibilityTreeBuilder
{
    public static void Rebuild(CartoViewModel vm, IList<CartoRosterTreeNode> roots)
    {
        CartoRosterTreeBuilder.Rebuild(vm, roots, (_, _) => true);

        foreach (var userNode in roots)
            AppendCharacterNodes(userNode);
    }

    private static void AppendCharacterNodes(CartoRosterTreeNode node)
    {
        if (node.Kind == CartoRosterNodeKind.Category)
        {
            foreach (var ch in node.CategoryCharacters
                         .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase))
            {
                node.Children.Add(new CartoRosterTreeNode
                {
                    Kind = CartoRosterNodeKind.Character,
                    User = node.User,
                    Account = node.Account,
                    Category = node.Category,
                    Character = ch,
                    Title = ch.Name,
                    Depth = node.Depth + 1,
                });
            }
        }

        foreach (var child in node.Children.Where(c => c.Kind != CartoRosterNodeKind.Character).ToList())
            AppendCharacterNodes(child);
    }
}
