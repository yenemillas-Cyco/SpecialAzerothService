using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SpecialAzerothService.Core.Models.WowSync;

namespace WindowsOrganiserApp.Controls;

public static class WowItemTooltipBuilder
{
    private static readonly Brush TooltipBg = new SolidColorBrush(Color.FromRgb(0x0E, 0x0E, 0x0E));
    private static readonly Brush TooltipBorder = new SolidColorBrush(Color.FromRgb(0x4A, 0x4A, 0x4A));
    private static readonly Brush TextWhite = new SolidColorBrush(Colors.White);
    private static readonly Brush TextYellow = new SolidColorBrush(Color.FromRgb(0xFF, 0xD1, 0x00));
    private static readonly Brush TextGreen = new SolidColorBrush(Color.FromRgb(0x1E, 0xFF, 0x00));
    public static ToolTip Create(WowItem item, WowheadItemDetails? details)
    {
        var quality = details?.Quality ?? item.Quality;
        var name = details?.Name ?? item.Name;
        var qualityBrush = BrushFromQuality(quality);

        var panel = new StackPanel { MaxWidth = 280 };

        panel.Children.Add(new TextBlock
        {
            Text = name,
            FontFamily = new FontFamily("Georgia"),
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = qualityBrush,
            TextWrapping = TextWrapping.Wrap
        });

        if (details?.ItemLevel is int ilvl)
        {
            panel.Children.Add(new TextBlock
            {
                Text = $"Niveau d'objet {ilvl}",
                FontFamily = new FontFamily("Georgia"),
                FontSize = 12,
                Foreground = TextYellow,
                Margin = new Thickness(0, 2, 0, 0)
            });
        }

        foreach (var line in details?.ExtraLines ?? [])
        {
            panel.Children.Add(new TextBlock
            {
                Text = line,
                FontFamily = new FontFamily("Georgia"),
                FontSize = 12,
                Foreground = TextWhite,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 1, 0, 0)
            });
        }

        if (details?.MaxStack is int maxStack)
        {
            panel.Children.Add(new TextBlock
            {
                Text = $"Empilement maxi: {maxStack}",
                FontFamily = new FontFamily("Georgia"),
                FontSize = 12,
                Foreground = TextWhite,
                Margin = new Thickness(0, 4, 0, 0)
            });
        }

        if (item.Count > 1)
        {
            panel.Children.Add(new TextBlock
            {
                Text = $"Vous en avez : {item.Count}",
                FontFamily = new FontFamily("Georgia"),
                FontSize = 12,
                Foreground = TextGreen,
                Margin = new Thickness(0, 4, 0, 0)
            });
        }

        if (details != null && (details.SellGold > 0 || details.SellSilver > 0 || details.SellCopper > 0))
            panel.Children.Add(BuildSellPriceRow(details));

        return new ToolTip
        {
            Content = new Border
            {
                Background = TooltipBg,
                BorderBrush = TooltipBorder,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 6, 8, 6),
                Child = panel
            },
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0)
        };
    }

    private static UIElement BuildSellPriceRow(WowheadItemDetails details)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 6, 0, 0)
        };

        row.Children.Add(new TextBlock
        {
            Text = "Prix de Vente: ",
            FontFamily = new FontFamily("Georgia"),
            FontSize = 12,
            Foreground = TextWhite,
            VerticalAlignment = VerticalAlignment.Center
        });

        if (details.SellGold > 0 || details.SellSilver > 0 || details.SellCopper > 0)
        {
            var copper = details.SellGold * 10000L + details.SellSilver * 100L + details.SellCopper;
            row.Children.Add(WowCurrencyDisplay.Build(copper, iconSize: 14, fontSize: 12));
        }

        return row;
    }

    private static Brush BrushFromQuality(int quality)
    {
        var hex = quality switch
        {
            0 => "#9D9D9D",
            1 => "#FFFFFF",
            2 => "#1EFF00",
            3 => "#0070DD",
            4 => "#A335EE",
            5 => "#FF8000",
            _ => "#E8D5A3"
        };

        try
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }
        catch
        {
            return TextWhite;
        }
    }
}
