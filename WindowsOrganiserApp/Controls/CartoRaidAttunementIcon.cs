using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SpecialAzerothService.Core.Models.Carto;
using SpecialAzerothService.Core.Models.WowSync;

namespace WindowsOrganiserApp.Controls;

public static class CartoRaidAttunementIcon
{
    private static readonly Color AttunedBorder = Color.FromRgb(72, 196, 96);
    private static readonly Color NotAttunedBorder = Color.FromRgb(210, 72, 72);

    public static Border Create(
        RaidAttunementDefinition definition,
        bool isAttuned,
        double size = 22,
        Faction? faction = null)
    {
        var iconItem = new WowItem { ItemId = definition.IconItemId, Name = definition.NameFr };
        var innerSize = Math.Max(12, size - 6);
        var icon = CartoMapQuestIcon.Create(iconItem, innerSize, bordered: false);
        var questId = definition.ResolveQuestId(faction);
        var questHint = questId > 0 ? $"quête {questId}" : "quête d'accès";

        var tip = isAttuned
            ? $"{definition.NameFr} — accès OK ({questHint})"
            : $"{definition.NameFr} — non attuné ({questHint})";

        return new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(3),
            BorderBrush = new SolidColorBrush(isAttuned ? AttunedBorder : NotAttunedBorder),
            BorderThickness = new Thickness(2),
            Background = new SolidColorBrush(Color.FromRgb(24, 18, 12)),
            Padding = new Thickness(1),
            Margin = new Thickness(0, 0, 4, 0),
            ToolTip = tip,
            Child = icon,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    public static void PreloadIcons()
    {
        if (WowItemSlot.LookupService == null)
            return;

        foreach (var def in RaidAttunementCatalog.All)
            CartoMapQuestIcon.PreloadItemIcon(def.IconItemId);
    }
}
