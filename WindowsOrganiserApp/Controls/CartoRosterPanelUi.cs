using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SpecialAzerothService.Core.Models.Carto;

namespace WindowsOrganiserApp.Controls;

/// <summary>En-têtes et badges du volet personnages (roster).</summary>
public static class CartoRosterPanelUi
{
    public const double PanelWidth = 432;
    public const double CountColumnWidth = 34;
    public const double VisColumnWidth = 26;
    public static T StretchWidth<T>(T element) where T : FrameworkElement
    {
        element.HorizontalAlignment = HorizontalAlignment.Stretch;
        return element;
    }

    public static Expander StretchExpander(Expander expander)
    {
        expander.HorizontalAlignment = HorizontalAlignment.Stretch;
        expander.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        return expander;
    }

    public static Brush GetCategoryAccent(CharacterStatus category) => category switch
    {
        CharacterStatus.Main => new SolidColorBrush(Color.FromRgb(218, 175, 70)),
        CharacterStatus.Banque => new SolidColorBrush(Color.FromRgb(120, 175, 220)),
        CharacterStatus.TpBoy => new SolidColorBrush(Color.FromRgb(160, 130, 210)),
        CharacterStatus.ClicBoys => new SolidColorBrush(Color.FromRgb(110, 190, 150)),
        _ => new SolidColorBrush(Color.FromRgb(180, 160, 120))
    };

    public static string GetCategoryGlyph(CharacterStatus category) => category switch
    {
        CharacterStatus.Main => "⚔",
        CharacterStatus.Banque => "🏦",
        CharacterStatus.TpBoy => "◇",
        CharacterStatus.ClicBoys => "✦",
        _ => "•"
    };

    public static Border BuildCountBadge(int count, Brush? accent = null)
    {
        var c = accent is SolidColorBrush sb ? sb.Color : Color.FromRgb(200, 170, 80);
        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(48, c.R, c.G, c.B)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(120, c.R, c.G, c.B)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(6, 2, 6, 2),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            MinWidth = 22,
            Child = new TextBlock
            {
                Text = count.ToString(),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = accent ?? new SolidColorBrush(Color.FromRgb(230, 210, 150)),
                TextAlignment = TextAlignment.Center
            }
        };
    }

    public static Grid BuildAlignedRightRail(int count, Brush? countAccent, long totalGoldCopper = 0, params UIElement[] trailingControls)
    {
        var grid = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        var col = 0;
        if (totalGoldCopper > 0)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var goldHost = WowCurrencyDisplay.Build(totalGoldCopper, iconSize: 13, fontSize: 10);
            goldHost.Margin = new Thickness(0, 0, 8, 0);
            goldHost.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(goldHost, col++);
            grid.Children.Add(goldHost);
        }

        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(CountColumnWidth) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(VisColumnWidth) });

        var badgeHost = new Grid { HorizontalAlignment = HorizontalAlignment.Center };
        badgeHost.Children.Add(BuildCountBadge(count, countAccent));
        Grid.SetColumn(badgeHost, col++);
        grid.Children.Add(badgeHost);

        var actionsHost = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        foreach (var ctrl in trailingControls)
            actionsHost.Children.Add(ctrl);

        Grid.SetColumn(actionsHost, col);
        grid.Children.Add(actionsHost);
        return grid;
    }

    public static Grid BuildCharacterCardActionsRail(UIElement visibilityToggle)
    {
        var grid = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Width = CountColumnWidth + VisColumnWidth
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(CountColumnWidth) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(VisColumnWidth) });

        Grid.SetColumn(visibilityToggle, 1);
        grid.Children.Add(visibilityToggle);
        return grid;
    }

    public static Grid BuildCategoryTitleRow(
        CharacterStatus category,
        string title,
        int characterCount,
        UIElement visibilityToggle,
        long totalGoldCopper = 0)
    {
        var accent = GetCategoryAccent(category);
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var stripe = new Border
        {
            Background = accent,
            CornerRadius = new CornerRadius(2),
            Margin = new Thickness(0, 2, 6, 2)
        };
        Grid.SetColumn(stripe, 0);
        grid.Children.Add(stripe);

        var glyph = new TextBlock
        {
            Text = GetCategoryGlyph(category),
            FontSize = 12,
            Foreground = accent,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0)
        };
        Grid.SetColumn(glyph, 1);
        grid.Children.Add(glyph);

        var titleBlock = new TextBlock
        {
            Text = title,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = accent,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(titleBlock, 2);
        grid.Children.Add(titleBlock);

        var right = BuildAlignedRightRail(characterCount, accent, totalGoldCopper, visibilityToggle);
        Grid.SetColumn(right, 3);
        grid.Children.Add(right);
        return StretchWidth(grid);
    }

    public static Grid BuildUserTitleRow(
        string userName,
        Brush nameBrush,
        int characterCount,
        UIElement visibilityToggle,
        long totalGoldCopper = 0)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var stripe = new Border
        {
            Background = nameBrush,
            CornerRadius = new CornerRadius(2),
            Margin = new Thickness(0, 2, 8, 2)
        };
        Grid.SetColumn(stripe, 0);
        grid.Children.Add(stripe);

        var title = new TextBlock
        {
            Text = userName,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = nameBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(title, 1);
        grid.Children.Add(title);

        var right = BuildAlignedRightRail(characterCount, nameBrush, totalGoldCopper, visibilityToggle);
        Grid.SetColumn(right, 2);
        grid.Children.Add(right);
        return StretchWidth(grid);
    }

    private static readonly Brush OwnerGoldBorder = new SolidColorBrush(Color.FromArgb(140, 204, 162, 74));

    /// <summary>Bloc propriétaire : bordure or uniquement, sans fond teinté.</summary>
    public static Border WrapUserOwnerFrame(UIElement content)
    {
        if (content is Expander userExpander)
            StretchExpander(userExpander);
        else if (content is FrameworkElement fe)
            StretchWidth(fe);

        return StretchWidth(new Border
        {
            Background = Brushes.Transparent,
            BorderBrush = OwnerGoldBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(6, 6, 6, 4),
            Margin = new Thickness(0, 0, 0, 8),
            Child = content
        });
    }

    /// <summary>Cadre catégorie (couleur distincte du propriétaire).</summary>
    public static Border WrapCategoryFrame(CharacterStatus category, UIElement content)
    {
        var accent = GetCategoryAccent(category);
        var c = accent is SolidColorBrush sb ? sb.Color : Colors.Gray;
        if (content is FrameworkElement fe)
            StretchWidth(fe);

        return StretchWidth(new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromArgb(90, c.R, c.G, c.B)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Background = new SolidColorBrush(Color.FromArgb(18, c.R, c.G, c.B)),
            Margin = new Thickness(0, 0, 0, 4),
            Child = content
        });
    }
}
