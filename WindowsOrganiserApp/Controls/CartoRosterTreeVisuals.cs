using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SpecialAzerothService.Core.Models.Carto;
using WindowsOrganiserApp.Models.Carto;

namespace WindowsOrganiserApp.Controls;

/// <summary>Templates visuels TreeView roster (rendu v3.0.14 : cadres or, comptes, catégories).</summary>
public static class CartoRosterTreeVisuals
{
    public static UIElement? Build(CartoRosterTreeNode node, CartoRosterTreeCallbacks callbacks) =>
        node.Kind switch
        {
            CartoRosterNodeKind.User => BuildUser(node, callbacks),
            CartoRosterNodeKind.Account => BuildAccount(node, callbacks),
            CartoRosterNodeKind.Category => BuildCategory(node, callbacks),
            _ => null
        };

    private static UIElement BuildUser(CartoRosterTreeNode node, CartoRosterTreeCallbacks cb)
    {
        var user = node.User!;
        var vm = cb.ViewModel;
        var userBrush = CartoCharacterPresentation.GetUserHeaderBrush(user, vm);

        var header = CartoRosterPanelUi.BuildUserTitleRow(
            user.Name,
            userBrush,
            null,
            node.GoldCopper,
            visibilityToggle: null);

        return CartoRosterPanelUi.WrapUserOwnerFrame(CartoRosterPanelUi.StretchWidth(header));
    }

    private static UIElement BuildAccount(CartoRosterTreeNode node, CartoRosterTreeCallbacks cb)
    {
        var accountBrush = new SolidColorBrush(Color.FromRgb(190, 175, 130));
        var header = CartoRosterPanelUi.BuildUserTitleRow(
            node.Account!.Name,
            accountBrush,
            null,
            node.GoldCopper,
            null);

        return new Border
        {
            Child = CartoRosterPanelUi.StretchWidth(header),
            Margin = new Thickness(6, 2, 0, 4),
            Background = Brushes.Transparent
        };
    }

    private static UIElement BuildCategory(CartoRosterTreeNode node, CartoRosterTreeCallbacks cb)
    {
        var user = node.User!;
        var category = node.Category!.Value;
        var vm = cb.ViewModel;

        var headerPanel = CartoRosterPanelUi.StretchWidth(new StackPanel());
        headerPanel.Children.Add(CartoRosterPanelUi.BuildCategoryTitleRow(
            category,
            node.Title,
            node.GoldCopper,
            visibilityToggle: null));

        if (node.CategoryCharacters.Count == 0 && node.CharacterCount > 0)
        {
            headerPanel.Children.Add(new TextBlock
            {
                Text = "Glissez un personnage ici",
                FontSize = 10,
                FontStyle = FontStyles.Italic,
                Foreground = new SolidColorBrush(Color.FromRgb(110, 105, 90)),
                Margin = new Thickness(12, 2, 0, 2)
            });
        }

        var body = CartoRosterPanelUi.StretchWidth(new StackPanel { Margin = new Thickness(0, 4, 0, 0) });
        if (cb.BuildCharacterCard != null)
        {
            foreach (var ch in node.CategoryCharacters)
                body.Children.Add(cb.BuildCharacterCard(ch));
        }

        var inner = CartoRosterPanelUi.StretchWidth(new StackPanel());
        inner.Children.Add(headerPanel);
        if (body.Children.Count > 0)
            inner.Children.Add(body);

        var shell = CartoRosterPanelUi.WrapCategoryFrame(category, inner);
        shell.Tag = category;
        shell.AllowDrop = true;
        shell.DragOver += (_, e) => cb.CategoryDragOver?.Invoke(category, e);
        shell.Drop += (_, e) => cb.CategoryDrop?.Invoke(category, e);
        shell.DragLeave += (_, _) => cb.CategoryDragLeave?.Invoke();
        return shell;
    }
}
