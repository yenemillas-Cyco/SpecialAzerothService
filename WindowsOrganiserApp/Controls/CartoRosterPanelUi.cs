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

    public static Grid BuildAlignedRightRail(long totalGoldCopper = 0, UIElement? trailing = null)
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
            goldHost.Margin = new Thickness(0, 0, trailing != null ? 8 : 0, 0);
            goldHost.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(goldHost, col++);
            grid.Children.Add(goldHost);
        }

        if (trailing != null)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            if (trailing is FrameworkElement trailingFe)
                trailingFe.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(trailing, col);
            grid.Children.Add(trailing);
        }

        return grid;
    }

    public static StackPanel BuildCooldownSummaryRail(int inProgress, int ready)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (inProgress > 0)
            row.Children.Add(BuildCooldownStatChip("⏳", inProgress, new SolidColorBrush(Color.FromRgb(190, 220, 255)), "CD en cours"));

        if (ready > 0)
            row.Children.Add(BuildCooldownReadyStatChip(ready));

        return row;
    }

    private static Border BuildCooldownStatChip(string icon, int count, Brush accent, string toolTip)
    {
        var c = accent is SolidColorBrush sb ? sb.Color : Color.FromRgb(190, 220, 255);
        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(48, c.R, c.G, c.B)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(120, c.R, c.G, c.B)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(6, 2, 6, 2),
            Margin = new Thickness(0, 0, 4, 0),
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = toolTip,
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children =
                {
                    new TextBlock
                    {
                        Text = icon,
                        FontSize = 11,
                        Foreground = accent,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 4, 0)
                    },
                    new TextBlock
                    {
                        Text = count.ToString(),
                        FontSize = 11,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = accent,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                }
            }
        };
    }

    private static Border BuildCooldownReadyStatChip(int count)
    {
        const byte g = 120, b = 140;
        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(56, 72, 190, 108)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(g, 230, b)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(6, 2, 6, 2),
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = "CD prêts",
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children =
                {
                    new Border
                    {
                        Width = 14,
                        Height = 14,
                        CornerRadius = new CornerRadius(7),
                        Background = new SolidColorBrush(Color.FromArgb(56, 72, 190, 108)),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(g, 230, b)),
                        BorderThickness = new Thickness(1),
                        Margin = new Thickness(0, 0, 4, 0),
                        Child = new TextBlock
                        {
                            Text = "✓",
                            FontSize = 10,
                            FontWeight = FontWeights.Bold,
                            Foreground = new SolidColorBrush(Color.FromRgb(g, 230, b)),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    },
                    new TextBlock
                    {
                        Text = count.ToString(),
                        FontSize = 11,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Color.FromRgb(g, 230, b)),
                        VerticalAlignment = VerticalAlignment.Center
                    }
                }
            }
        };
    }

    public static Grid BuildCategoryTitleRow(
        CharacterStatus category,
        string title,
        long totalGoldCopper = 0,
        UIElement? visibilityToggle = null)
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

        var right = BuildTitleRightRail(totalGoldCopper, visibilityToggle);
        if (right != null)
        {
            Grid.SetColumn(right, 3);
            grid.Children.Add(right);
        }

        return StretchWidth(grid);
    }

    public static Grid BuildUserTitleRow(
        string userName,
        Brush nameBrush,
        UIElement? rightRail = null,
        long totalGoldCopper = 0,
        UIElement? visibilityToggle = null)
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

        var right = rightRail != null
            ? BuildAlignedRightRail(totalGoldCopper, rightRail)
            : BuildTitleRightRail(totalGoldCopper, visibilityToggle);
        if (right != null)
        {
            Grid.SetColumn(right, 2);
            grid.Children.Add(right);
        }

        return StretchWidth(grid);
    }

    private static UIElement? BuildTitleRightRail(long totalGoldCopper, UIElement? visibilityToggle)
    {
        var gold = totalGoldCopper > 0 ? BuildAlignedRightRail(totalGoldCopper) : null;
        var hasGold = gold is { Children.Count: > 0 };
        if (!hasGold && visibilityToggle == null)
            return null;

        if (visibilityToggle == null)
            return gold;

        if (!hasGold)
            return visibilityToggle;

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        gold!.VerticalAlignment = VerticalAlignment.Center;
        gold.Margin = new Thickness(0, 0, 6, 0);
        row.Children.Add(gold);
        row.Children.Add(visibilityToggle);
        return row;
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
