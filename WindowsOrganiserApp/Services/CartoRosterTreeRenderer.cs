using System.Windows;
using System.Windows.Controls;
using WindowsOrganiserApp.Models.Carto;
using WindowsOrganiserApp.ViewModels;

namespace WindowsOrganiserApp.Services;

/// <summary>Peint l'arbre logique avec les expanders stylisés (rendu v3.0.14).</summary>
public static class CartoRosterTreeRenderer
{
    public delegate UIElement BuildUserExpanderFn(
        CartoUser user,
        IReadOnlyList<WowCharacter> userCharacters,
        UIElement content,
        bool cooldownPanel);

    public delegate Expander BuildAccountExpanderFn(
        CartoUser user,
        WowAccount account,
        StackPanel content,
        int buildId);

    public delegate Border BuildCategoryExpanderFn(
        CartoUser user,
        string title,
        CharacterStatus category,
        IReadOnlyList<WowCharacter> characters,
        int totalInCategory,
        string? accountId,
        int buildId);

    public sealed class Host
    {
        public required CartoViewModel ViewModel { get; init; }
        public required BuildUserExpanderFn BuildUserExpander { get; init; }
        public required BuildAccountExpanderFn BuildAccountExpander { get; init; }
        public required BuildCategoryExpanderFn BuildCategoryExpander { get; init; }
        public required Func<StackPanel, StackPanel> StretchPanel { get; init; }
    }

    public static void Render(
        StackPanel root,
        IReadOnlyList<CartoRosterTreeNode> treeRoots,
        Host host,
        int buildId,
        Func<int, bool> isBuildCurrent)
    {
        root.Children.Clear();

        if (treeRoots.Count == 0)
        {
            root.Children.Add(new TextBlock
            {
                Text = "Aucun personnage.\n⚙ (en haut) → WowSync + comptes WoW, puis Actualiser.",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11,
                Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(110, 105, 90)),
                Margin = new Thickness(4, 8, 4, 8)
            });
            return;
        }

        foreach (var userNode in treeRoots)
        {
            if (!isBuildCurrent(buildId))
                return;

            var user = userNode.User;
            if (user == null)
                continue;

            var userPanel = new StackPanel();
            host.StretchPanel(userPanel);

            foreach (var accountNode in userNode.Children)
            {
                if (!isBuildCurrent(buildId))
                    return;

                var account = accountNode.Account;
                if (account == null)
                    continue;

                var accountPanel = new StackPanel();
                host.StretchPanel(accountPanel);

                foreach (var categoryNode in accountNode.Children)
                {
                    if (!isBuildCurrent(buildId))
                        return;

                    if (categoryNode.Category is not { } category)
                        continue;

                    var characters = GetCategoryCharacters(categoryNode);
                    accountPanel.Children.Add(host.BuildCategoryExpander(
                        user,
                        categoryNode.Title,
                        category,
                        characters,
                        categoryNode.CharacterCount,
                        account.Id,
                        buildId));
                }

                userPanel.Children.Add(host.BuildAccountExpander(user, account, accountPanel, buildId));
            }

            var userCharacters = CollectCharacters(userNode);
            root.Children.Add(host.BuildUserExpander(user, userCharacters, userPanel, cooldownPanel: false));
        }
    }

    private static IReadOnlyList<WowCharacter> GetCategoryCharacters(CartoRosterTreeNode categoryNode)
    {
        if (categoryNode.CategoryCharacters.Count > 0)
            return categoryNode.CategoryCharacters;

        return categoryNode.Children
            .Where(n => n.Character != null)
            .Select(n => n.Character!)
            .ToList();
    }

    private static List<WowCharacter> CollectCharacters(CartoRosterTreeNode node)
    {
        var list = new List<WowCharacter>();
        CollectCharactersCore(node, list);
        return list;
    }

    private static void CollectCharactersCore(CartoRosterTreeNode node, List<WowCharacter> list)
    {
        list.AddRange(node.CategoryCharacters);
        foreach (var child in node.Children)
            CollectCharactersCore(child, list);
    }
}
