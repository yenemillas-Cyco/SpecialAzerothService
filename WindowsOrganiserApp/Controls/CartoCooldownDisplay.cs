using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SpecialAzerothService.Core.Models.Carto;
using SpecialAzerothService.Core.Models.WowSync;

namespace WindowsOrganiserApp.Controls;

/// <summary>Barres de progression pour les cooldowns métiers (aperçu portrait + popup).</summary>
public static class CartoCooldownDisplay
{
    public const string PanelTag = "carto-cd-panel";

    private static readonly SolidColorBrush TrackBrush = new(Color.FromArgb(200, 12, 14, 20));
    private static readonly SolidColorBrush TrackEdgeBrush = new(Color.FromArgb(120, 60, 70, 90));
    private static readonly SolidColorBrush FillReadyBrush = new(Color.FromRgb(72, 190, 108));
    private static readonly SolidColorBrush LabelBrush = new(Color.FromRgb(200, 215, 235));
    private static readonly SolidColorBrush ReadyBrush = new(Color.FromRgb(120, 230, 140));
    private static readonly SolidColorBrush RemainingBrush = new(Color.FromRgb(140, 200, 255));
    private static readonly SolidColorBrush CompactTimeBrush = new(Color.FromRgb(220, 230, 245));

    private const int MaxPortraitBars = 3;

    private sealed class CooldownRowState(
        CooldownEntry entry,
        ColumnDefinition fillColumn,
        ColumnDefinition emptyColumn,
        Border fillBorder,
        TextBlock? remainingBlock,
        TextBlock? compactTimeBlock,
        bool showProgressRatio = false)
    {
        public CooldownEntry Entry { get; } = entry;
        public ColumnDefinition FillColumn { get; } = fillColumn;
        public ColumnDefinition EmptyColumn { get; } = emptyColumn;
        public Border FillBorder { get; } = fillBorder;
        public TextBlock? RemainingBlock { get; } = remainingBlock;
        public TextBlock? CompactTimeBlock { get; } = compactTimeBlock;
        public bool ShowProgressRatio { get; } = showProgressRatio;
    }

    public static StackPanel? BuildPanel(WowCharacter ch, double labelFontSize = 10, WowCharacterData? sync = null)
    {
        var cds = ResolveDisplayCooldowns(ch, sync);
        if (cds.Count == 0)
            return null;

        var panel = new StackPanel { Tag = PanelTag };
        foreach (var cd in cds)
            panel.Children.Add(BuildRow(cd, labelFontSize, compact: false));

        return panel;
    }

    /// <summary>Bandeau CD pleine largeur sur la carte roster.</summary>
    public static UIElement? BuildRosterCardStrip(WowCharacter ch, WowCharacterData? sync = null)
    {
        var cds = ResolveDisplayCooldowns(ch, sync).Take(MaxPortraitBars).ToList();
        if (cds.Count == 0)
            return null;

        var stack = new StackPanel
        {
            Tag = PanelTag,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 2, 0, 8)
        };
        foreach (var cd in cds)
            stack.Children.Add(BuildRow(cd, 10, compact: true, rosterList: true));

        return stack;
    }

    /// <summary>CD configurés + CD WowSync en cours (sans exiger LastUsed sur la fiche).</summary>
    public static List<CooldownEntry> ResolveDisplayCooldowns(WowCharacter ch, WowCharacterData? sync = null)
    {
        var byType = new Dictionary<CooldownType, CooldownEntry>();
        foreach (var cd in ch.Cooldowns)
            byType[cd.Type] = cd;

        if (sync != null)
        {
            foreach (var syncCd in sync.Cooldowns)
            {
                if (syncCd.IsReady)
                    continue;

                var type = MapSyncCooldownKey(syncCd.Key, syncCd.Name);
                if (type == null)
                    continue;

                if (!byType.TryGetValue(type.Value, out var entry)
                    && CooldownGroups.IsAlchemyTransmute(type.Value))
                    entry = byType.Values.FirstOrDefault(c => CooldownGroups.IsAlchemyTransmute(c.Type));

                if (entry == null)
                {
                    entry = new CooldownEntry { Type = type.Value };
                    byType[type.Value] = entry;
                }
                else if (CooldownGroups.IsAlchemyTransmute(type.Value))
                {
                    var staleKey = byType.FirstOrDefault(kv => ReferenceEquals(kv.Value, entry)).Key;
                    if (!staleKey.Equals(default) && staleKey != type.Value)
                        byType.Remove(staleKey);
                    entry.Type = type.Value;
                    byType[type.Value] = entry;
                }

                if (syncCd.ReadyAtUtc is { } readyAt)
                {
                    entry.ReadyAtOverride = readyAt;
                    var remaining = readyAt - DateTime.UtcNow;
                    if (remaining > TimeSpan.Zero)
                    {
                        var total = entry.Duration;
                        if (type == CooldownType.Arcanite && remaining > TimeSpan.FromHours(25))
                            total = TimeSpan.FromHours(48);
                        else if (remaining > total)
                            total = remaining;

                        entry.LastUsed = readyAt - total;
                    }
                    else if (entry.LastUsed == null)
                        entry.LastUsed = readyAt - entry.Duration;
                }
            }
        }

        var merged = byType.Values.ToList();
        CooldownGroups.NormalizeAlchemyCooldowns(merged);

        return merged
            .OrderBy(c => c.IsReady ? 1 : 0)
            .ThenBy(c => c.TimeRemaining ?? TimeSpan.Zero)
            .ToList();
    }

    private static CooldownType? MapSyncCooldownKey(string key, string? syncName = null)
    {
        if (CooldownGroups.MapAlchemySyncKey(key, syncName) is { } alchemy)
            return alchemy;

        return key.ToLowerInvariant() switch
        {
            "mooncloth" => CooldownType.Mooncloth,
            "salt" => CooldownType.Sel_raffine,
            _ => null
        };
    }

    private static SolidColorBrush AccentBrushFor(CooldownType type) => type switch
    {
        CooldownType.Sel_raffine => new SolidColorBrush(Color.FromRgb(210, 185, 90)),
        CooldownType.Mooncloth or CooldownType.Etoffe_lunaire => new SolidColorBrush(Color.FromRgb(175, 130, 220)),
        CooldownType.Arcanite => new SolidColorBrush(Color.FromRgb(110, 165, 230)),
        CooldownType.Transmute_Elementaire => new SolidColorBrush(Color.FromRgb(230, 130, 75)),
        _ => new SolidColorBrush(Color.FromRgb(100, 170, 230))
    };

    private static SolidColorBrush FillBrushFor(CooldownType type, bool ready) => type switch
    {
        CooldownType.Sel_raffine => ready
            ? FillReadyBrush
            : new SolidColorBrush(Color.FromRgb(200, 175, 85)),
        CooldownType.Mooncloth or CooldownType.Etoffe_lunaire => ready
            ? FillReadyBrush
            : new SolidColorBrush(Color.FromRgb(150, 110, 195)),
        CooldownType.Arcanite => ready
            ? FillReadyBrush
            : new SolidColorBrush(Color.FromRgb(95, 145, 215)),
        CooldownType.Transmute_Elementaire => ready
            ? FillReadyBrush
            : new SolidColorBrush(Color.FromRgb(215, 115, 70)),
        _ => ready
            ? FillReadyBrush
            : new SolidColorBrush(Color.FromRgb(70, 150, 230))
    };

    public static void UpdateAll(DependencyObject? root)
    {
        if (root == null)
            return;

        foreach (var panel in FindTagged<Panel>(root, PanelTag))
            UpdatePanel(panel);
    }

    private static void UpdatePanel(Panel panel)
    {
        foreach (var child in panel.Children)
        {
            if (child is Grid grid)
            {
                foreach (var row in grid.Children.OfType<FrameworkElement>())
                {
                    if (row.Tag is CooldownRowState nested)
                        ApplyState(nested);
                }
            }

            if (child is FrameworkElement { Tag: CooldownRowState state })
                ApplyState(state);
        }
    }

    private static void ApplyState(CooldownRowState state)
    {
        var cd = state.Entry;
        var fraction = cd.IsReady ? 1.0 : cd.ElapsedFraction;
        SetBarWidths(state.FillColumn, state.EmptyColumn, fraction);
        state.FillBorder.Background = FillBrushFor(cd.Type, cd.IsReady);

        if (state.RemainingBlock != null)
        {
            state.RemainingBlock.Text = cd.IsReady ? "Prêt" : FormatProgressRatio(cd);
            state.RemainingBlock.Foreground = cd.IsReady ? ReadyBrush : RemainingBrush;
        }

        if (state.CompactTimeBlock != null)
        {
            state.CompactTimeBlock.Text = cd.IsReady
                ? "Prêt"
                : state.ShowProgressRatio
                    ? FormatProgressRatio(cd, compact: true)
                    : FormatTimeRemaining(cd.TimeRemaining, compact: true);
            state.CompactTimeBlock.Foreground = cd.IsReady ? ReadyBrush : CompactTimeBrush;
        }
    }

    private static void SetBarWidths(ColumnDefinition fillColumn, ColumnDefinition emptyColumn, double elapsedFraction)
    {
        const double min = 0.001;
        var fill = Math.Clamp(elapsedFraction, min, 1);
        var empty = Math.Max(min, 1 - fill);
        fillColumn.Width = new GridLength(fill, GridUnitType.Star);
        emptyColumn.Width = new GridLength(empty, GridUnitType.Star);
    }

    private static UIElement BuildRow(
        CooldownEntry cd,
        double labelFontSize,
        bool compact,
        bool portraitInset = false,
        bool rosterList = false)
    {
        var fillColumn = new ColumnDefinition();
        var emptyColumn = new ColumnDefinition();
        SetBarWidths(fillColumn, emptyColumn, cd.IsReady ? 1 : cd.ElapsedFraction);

        var trackHeight = portraitInset ? 3.5 : rosterList ? 11 : compact ? 5 : 8;
        var trackGrid = new Grid
        {
            Height = trackHeight,
            ClipToBounds = true
        };
        trackGrid.ColumnDefinitions.Add(fillColumn);
        trackGrid.ColumnDefinitions.Add(emptyColumn);

        var fill = new Border
        {
            Background = FillBrushFor(cd.Type, cd.IsReady),
            CornerRadius = new CornerRadius(trackHeight / 2, 0, 0, trackHeight / 2),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        Grid.SetColumn(fill, 0);
        trackGrid.Children.Add(fill);

        var trackShell = new Border
        {
            Background = TrackBrush,
            BorderBrush = TrackEdgeBrush,
            BorderThickness = portraitInset ? new Thickness(0) : new Thickness(1),
            CornerRadius = new CornerRadius(trackHeight / 2),
            Padding = new Thickness(0),
            Child = trackGrid
        };

        TextBlock? remaining = null;
        TextBlock? compactTime = null;

        if (!compact)
        {
            remaining = new TextBlock
            {
                FontSize = labelFontSize,
                Foreground = cd.IsReady ? ReadyBrush : RemainingBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Text = cd.IsReady ? "Prêt" : FormatProgressRatio(cd)
            };

            var header = new Grid { Margin = new Thickness(0, 0, 0, 3) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var name = new TextBlock
            {
                Text = CdShortLabel(cd.Type),
                FontSize = labelFontSize,
                Foreground = LabelBrush,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = cd.Type.DisplayName()
            };
            Grid.SetColumn(name, 0);
            Grid.SetColumn(remaining, 1);
            header.Children.Add(name);
            header.Children.Add(remaining);

            var row = new StackPanel { Margin = new Thickness(0, 0, 0, 6) };
            row.Children.Add(header);
            row.Children.Add(trackShell);
            row.Tag = new CooldownRowState(cd, fillColumn, emptyColumn, fill, remaining, null);
            return row;
        }

        compactTime = new TextBlock
        {
            Text = cd.IsReady
                ? "Prêt"
                : rosterList
                    ? FormatProgressRatio(cd, compact: true)
                    : FormatTimeRemaining(cd.TimeRemaining, compact: true),
            FontSize = rosterList ? 10 : portraitInset ? 7 : 8,
            FontWeight = FontWeights.SemiBold,
            Foreground = cd.IsReady ? ReadyBrush : CompactTimeBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(rosterList ? 6 : portraitInset ? 3 : 4, 0, 0, 0),
            TextAlignment = TextAlignment.Right,
            MinWidth = rosterList ? 72 : portraitInset ? 28 : 34
        };

        var typeLabel = new TextBlock
        {
            Text = rosterList ? CdRosterLabel(cd.Type) : CdTinyLabel(cd.Type),
            FontSize = rosterList ? 10 : 7,
            FontWeight = rosterList ? FontWeights.SemiBold : FontWeights.Bold,
            Foreground = AccentBrushFor(cd.Type),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = rosterList ? new Thickness(0, 0, 6, 0) : new Thickness(0),
            TextAlignment = rosterList ? TextAlignment.Left : TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = rosterList ? 88 : double.PositiveInfinity,
            ToolTip = cd.Type.DisplayName()
        };

        var barRow = new Grid
        {
            Margin = portraitInset
                ? new Thickness(2, 1, 2, 1)
                : new Thickness(0, 0, 0, rosterList ? 5 : 2),
            ToolTip = $"{cd.Type.DisplayName()} — {(cd.IsReady ? "Prêt" : FormatProgressRatio(cd))}"
        };

        if (rosterList)
        {
            barRow.HorizontalAlignment = HorizontalAlignment.Stretch;
            barRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            barRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            barRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            trackShell.HorizontalAlignment = HorizontalAlignment.Stretch;
            trackShell.MinHeight = trackHeight;
            trackShell.CornerRadius = new CornerRadius(trackHeight / 2);
            Grid.SetColumn(typeLabel, 0);
            Grid.SetColumn(trackShell, 1);
            Grid.SetColumn(compactTime, 2);
            barRow.Children.Add(typeLabel);
            barRow.Children.Add(trackShell);
            barRow.Children.Add(compactTime);
        }
        else
        {
            var accent = new Border
            {
                Width = portraitInset ? 2 : 3,
                Background = AccentBrushFor(cd.Type),
                CornerRadius = new CornerRadius(1),
                Margin = new Thickness(0, 0, portraitInset ? 2 : 3, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            barRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            barRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            barRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            barRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(typeLabel, 0);
            Grid.SetColumn(accent, 1);
            Grid.SetColumn(trackShell, 2);
            Grid.SetColumn(compactTime, 3);
            barRow.Children.Add(typeLabel);
            barRow.Children.Add(accent);
            barRow.Children.Add(trackShell);
            barRow.Children.Add(compactTime);
        }

        barRow.Tag = new CooldownRowState(cd, fillColumn, emptyColumn, fill, null, compactTime, rosterList);
        return barRow;
    }

    private static string CdRosterLabel(CooldownType type)
    {
        if (CooldownGroups.IsAlchemyTransmute(type) && type != CooldownType.Arcanite)
            return CdShortLabel(type);

        return type switch
        {
            CooldownType.Arcanite => "Arcanite",
            CooldownType.Transmute_Elementaire => "Élémentaire",
            CooldownType.Mooncloth or CooldownType.Etoffe_lunaire => "Étoffe lunaire",
            CooldownType.Sel_raffine => "Sel raffiné",
            _ => CdShortLabel(type)
        };
    }

    private static string CdTinyLabel(CooldownType type) => type switch
    {
        CooldownType.Arcanite => "Arc",
        CooldownType.Transmute_Elementaire => "Él",
        CooldownType.Mooncloth => "Lun",
        CooldownType.Sel_raffine => "Sel",
        _ => "CD"
    };

    private static string CdShortLabel(CooldownType type) => type switch
    {
        CooldownType.Arcanite => "Arcanite",
        CooldownType.Transmute_Elementaire => "Élémentaire",
        CooldownType.Air_to_Fire => "Air → Feu",
        CooldownType.Fire_to_Earth => "Feu → Terre",
        CooldownType.Earth_to_Water => "Terre → Eau",
        CooldownType.Water_to_Air => "Eau → Air",
        CooldownType.Undeath_to_Water => "Mort → Eau",
        CooldownType.Water_to_Undeath => "Eau → Mort",
        CooldownType.Life_to_Earth => "Vie → Terre",
        CooldownType.Earth_to_Life => "Terre → Vie",
        CooldownType.Mooncloth => "Lunaire",
        CooldownType.Sel_raffine => "Sel",
        _ => type.ToString().Replace('_', ' ')
    };

    private static IEnumerable<T> FindTagged<T>(DependencyObject parent, string tag)
        where T : FrameworkElement
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T { Tag: string s } fe && s == tag)
                yield return fe;

            foreach (var nested in FindTagged<T>(child, tag))
                yield return nested;
        }
    }

    private static string FormatProgressRatio(CooldownEntry cd, bool compact = false)
    {
        if (cd.IsReady)
            return "Prêt";

        var remaining = cd.TimeRemaining ?? TimeSpan.Zero;
        var elapsed = cd.Duration - remaining;
        if (elapsed < TimeSpan.Zero)
            elapsed = TimeSpan.Zero;

        return $"{FormatDuration(elapsed, compact)} / {FormatDuration(cd.EffectiveDuration, compact)}";
    }

    private static string FormatDuration(TimeSpan span, bool compact = false)
    {
        if (span.TotalDays >= 1)
        {
            var days = (int)span.TotalDays;
            if (compact && span - TimeSpan.FromDays(days) < TimeSpan.FromHours(1))
                return $"{days}j";

            return compact
                ? $"{days}j{span.Hours:D2}"
                : $"{days} j {span.Hours:D2} h";
        }

        if (span.TotalHours >= 1)
            return compact
                ? $"{(int)span.TotalHours}h{span.Minutes:D2}"
                : $"{(int)span.TotalHours} h {span.Minutes:D2} min";

        if (span.TotalSeconds < 60)
            return compact ? $"{span.Seconds}s" : $"{span.Seconds} s";

        return compact
            ? $"{(int)span.TotalMinutes}m"
            : $"{(int)span.TotalMinutes} min {span.Seconds:D2} s";
    }

    private static string FormatTimeRemaining(TimeSpan? ts, bool compact = false)
    {
        if (ts == null)
            return compact ? "—" : "—";

        return FormatDuration(ts.Value, compact);
    }
}
