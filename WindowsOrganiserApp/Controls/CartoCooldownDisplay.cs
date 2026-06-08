using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SpecialAzerothService.Core.Models.Carto;
using SpecialAzerothService.Core.Models.WowSync;
using SpecialAzerothService.Core.Services;

namespace WindowsOrganiserApp.Controls;

/// <summary>Barres de progression pour les cooldowns métiers (aperçu portrait + popup).</summary>
public static class CartoCooldownDisplay
{
    public const string PanelTag = "carto-cd-panel";

    private static readonly SolidColorBrush TrackBrush = new(Color.FromArgb(220, 18, 20, 28));
    private static readonly SolidColorBrush TrackEdgeBrush = new(Color.FromArgb(100, 70, 80, 100));
    private static readonly SolidColorBrush FillReadyBrush = new(Color.FromRgb(56, 168, 98));
    private static readonly SolidColorBrush LabelBrush = new(Color.FromRgb(200, 215, 235));
    private static readonly SolidColorBrush ReadyBrush = new(Color.FromRgb(120, 230, 140));
    private static readonly SolidColorBrush RemainingBrush = new(Color.FromRgb(190, 220, 255));
    private static readonly SolidColorBrush SubtextBrush = new(Color.FromRgb(150, 165, 185));

    private const double RosterLabelWidth = 72;
    private const double RosterStatusWidth = 88;
    private const double RosterTrackHeight = 13;

    private sealed class CooldownRowState(
        CooldownEntry entry,
        ColumnDefinition fillColumn,
        ColumnDefinition emptyColumn,
        Border fillBorder,
        Grid trackGrid,
        TextBlock? remainingBlock,
        TextBlock? compactTimeBlock,
        TextBlock? timeBlock = null,
        TextBlock? percentBlock = null,
        UIElement? readyBadge = null)
    {
        public CooldownEntry Entry { get; } = entry;
        public ColumnDefinition FillColumn { get; } = fillColumn;
        public ColumnDefinition EmptyColumn { get; } = emptyColumn;
        public Border FillBorder { get; } = fillBorder;
        public Grid TrackGrid { get; } = trackGrid;
        public TextBlock? RemainingBlock { get; } = remainingBlock;
        public TextBlock? CompactTimeBlock { get; } = compactTimeBlock;
        public TextBlock? TimeBlock { get; } = timeBlock;
        public TextBlock? PercentBlock { get; } = percentBlock;
        public UIElement? ReadyBadge { get; } = readyBadge;
    }

    public static StackPanel? BuildPanel(WowCharacter ch, double labelFontSize = 10, WowCharacterData? sync = null)
    {
        var cds = ResolveDisplayCooldowns(ch, sync);
        if (cds.Count == 0)
            return null;

        var panel = new StackPanel { Tag = PanelTag };
        foreach (var cd in cds)
            panel.Children.Add(BuildDetailRow(cd, labelFontSize));

        return panel;
    }

    public static UIElement? BuildRosterCardStrip(WowCharacter ch, WowCharacterData? sync = null)
    {
        var cds = ResolveDisplayCooldowns(ch, sync);
        if (cds.Count == 0)
            return null;

        var stack = new StackPanel
        {
            Tag = PanelTag,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 2, 0, 0)
        };
        foreach (var cd in cds)
            stack.Children.Add(BuildRosterCooldownRow(cd));

        return stack;
    }

    public static List<CooldownEntry> ResolveDisplayCooldowns(WowCharacter ch, WowCharacterData? sync = null)
    {
        var byType = new Dictionary<CooldownType, CooldownEntry>();
        foreach (var cd in ch.Cooldowns)
        {
            if (CartoProfessionCooldowns.IsCooldownEntryVisible(cd, ch, sync))
                byType[cd.Type] = cd;
        }

        if (sync != null)
        {
            foreach (var syncCd in sync.Cooldowns)
            {
                var type = CooldownGroups.MapSyncCooldownKey(syncCd.Key, syncCd.Name);
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

                CooldownSyncMerge.ApplySyncEntry(entry, syncCd, type.Value);
            }
        }

        CartoProfessionCooldowns.EnsureProfessionSlots(byType, ch, sync);

        var merged = byType.Values.ToList();
        CooldownGroups.NormalizeAlchemyCooldowns(merged);

        return merged
            .Where(cd => CartoProfessionCooldowns.IsCooldownEntryVisible(cd, ch, sync))
            .OrderBy(c => c.IsReady ? 1 : 0)
            .ThenBy(c => c.TimeRemaining ?? TimeSpan.Zero)
            .ToList();
    }

    public static bool HasDisplayableCooldowns(WowCharacter ch, WowCharacterData? sync = null) =>
        ResolveDisplayCooldowns(ch, sync).Count > 0;

    public static (int InProgress, int Ready) CountCooldownStatuses(
        IEnumerable<WowCharacter> characters,
        Func<WowCharacter, WowCharacterData?> getSync)
    {
        var inProgress = 0;
        var ready = 0;
        foreach (var ch in characters)
        {
            var sync = getSync(ch);
            foreach (var cd in ResolveDisplayCooldowns(ch, sync))
            {
                if (cd.IsReady)
                    ready++;
                else
                    inProgress++;
            }
        }

        return (inProgress, ready);
    }

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
            if (child is FrameworkElement { Tag: CooldownRowState state })
                ApplyState(state);
        }
    }

    private static void ApplyState(CooldownRowState state)
    {
        var cd = state.Entry;
        ApplyBarLayout(cd, state.FillBorder, state.FillColumn, state.EmptyColumn, state.TrackGrid);

        if (state.TimeBlock != null)
        {
            state.TimeBlock.Text = cd.IsReady ? "" : FormatTimeRemaining(cd.TimeRemaining);
            state.TimeBlock.Visibility = cd.IsReady ? Visibility.Collapsed : Visibility.Visible;
        }

        if (state.PercentBlock != null)
        {
            state.PercentBlock.Text = cd.IsReady ? "" : $"{GetRemainingPercent(cd)} % restant";
            state.PercentBlock.Visibility = cd.IsReady ? Visibility.Collapsed : Visibility.Visible;
        }

        if (state.ReadyBadge != null)
            state.ReadyBadge.Visibility = cd.IsReady ? Visibility.Visible : Visibility.Collapsed;

        var summary = FormatStatusSummary(cd);
        if (state.RemainingBlock != null)
        {
            state.RemainingBlock.Text = summary;
            state.RemainingBlock.Foreground = cd.IsReady ? ReadyBrush : RemainingBrush;
        }

        if (state.CompactTimeBlock != null)
        {
            state.CompactTimeBlock.Text = summary;
            state.CompactTimeBlock.Foreground = cd.IsReady ? ReadyBrush : RemainingBrush;
        }
    }

    private static void ApplyBarLayout(
        CooldownEntry cd,
        Border fill,
        ColumnDefinition fillColumn,
        ColumnDefinition emptyColumn,
        Grid trackGrid)
    {
        var trackHeight = trackGrid.Height > 0 ? trackGrid.Height : RosterTrackHeight;
        var outerRadius = trackHeight / 2;
        var innerRadius = Math.Max(1, outerRadius - 1);
        var elapsed = cd.IsReady ? 1.0 : cd.ElapsedFraction;
        var full = cd.IsReady || elapsed >= 0.995;

        fill.Background = cd.IsReady ? FillReadyBrush : FillBrushFor(cd.Type);

        if (full)
        {
            fillColumn.Width = new GridLength(1, GridUnitType.Star);
            emptyColumn.Width = new GridLength(0);
            Grid.SetColumn(fill, 0);
            Grid.SetColumnSpan(fill, 2);
            fill.CornerRadius = new CornerRadius(innerRadius);
            return;
        }

        Grid.SetColumnSpan(fill, 1);
        fill.CornerRadius = new CornerRadius(innerRadius, 0, 0, innerRadius);

        const double min = 0.001;
        var fillWeight = Math.Clamp(elapsed, min, 1);
        fillColumn.Width = new GridLength(fillWeight, GridUnitType.Star);
        emptyColumn.Width = new GridLength(Math.Max(min, 1 - fillWeight), GridUnitType.Star);
    }

    private static void SetBarWidths(ColumnDefinition fillColumn, ColumnDefinition emptyColumn, double elapsedFraction)
    {
        const double min = 0.001;
        var fill = Math.Clamp(elapsedFraction, min, 1);
        var empty = Math.Max(min, 1 - fill);
        fillColumn.Width = new GridLength(fill, GridUnitType.Star);
        emptyColumn.Width = new GridLength(empty, GridUnitType.Star);
    }

    private static UIElement BuildRosterCooldownRow(CooldownEntry cd)
    {
        var fillColumn = new ColumnDefinition();
        var emptyColumn = new ColumnDefinition();

        var fill = new Border { HorizontalAlignment = HorizontalAlignment.Stretch };

        var trackGrid = new Grid { Height = RosterTrackHeight, ClipToBounds = true };
        trackGrid.ColumnDefinitions.Add(fillColumn);
        trackGrid.ColumnDefinitions.Add(emptyColumn);
        trackGrid.Children.Add(fill);
        ApplyBarLayout(cd, fill, fillColumn, emptyColumn, trackGrid);

        var track = new Border
        {
            Background = TrackBrush,
            BorderBrush = TrackEdgeBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(RosterTrackHeight / 2),
            Child = trackGrid,
            VerticalAlignment = VerticalAlignment.Center,
            MinHeight = RosterTrackHeight,
            Margin = new Thickness(6, 0, 6, 0)
        };

        var timeBlock = new TextBlock
        {
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = RemainingBrush,
            HorizontalAlignment = HorizontalAlignment.Right,
            Text = FormatTimeRemaining(cd.TimeRemaining)
        };

        var percentBlock = new TextBlock
        {
            FontSize = 9,
            Foreground = SubtextBrush,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 1, 0, 0),
            Text = $"{GetRemainingPercent(cd)} % restant"
        };

        var runningStatus = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            Visibility = cd.IsReady ? Visibility.Collapsed : Visibility.Visible
        };
        runningStatus.Children.Add(timeBlock);
        runningStatus.Children.Add(percentBlock);

        var readyBadge = BuildReadyBadge();
        readyBadge.Visibility = cd.IsReady ? Visibility.Visible : Visibility.Collapsed;

        var statusHost = new Grid { Width = RosterStatusWidth, MinWidth = RosterStatusWidth };
        statusHost.Children.Add(runningStatus);
        statusHost.Children.Add(readyBadge);

        var typeLabel = new TextBlock
        {
            Text = CdRosterLabel(cd.Type),
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = AccentBrushFor(cd.Type),
            VerticalAlignment = VerticalAlignment.Center,
            Width = RosterLabelWidth,
            TextTrimming = TextTrimming.CharacterEllipsis,
            ToolTip = cd.Type.DisplayName()
        };

        var row = new Grid
        {
            Margin = new Thickness(0, 0, 0, 6),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ToolTip = $"{cd.Type.DisplayName()} — {FormatStatusSummary(cd, longForm: true)}"
        };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(RosterLabelWidth) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 48 });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(RosterStatusWidth) });
        Grid.SetColumn(typeLabel, 0);
        Grid.SetColumn(track, 1);
        Grid.SetColumn(statusHost, 2);
        row.Children.Add(typeLabel);
        row.Children.Add(track);
        row.Children.Add(statusHost);

        row.Tag = new CooldownRowState(cd, fillColumn, emptyColumn, fill, trackGrid, null, null, timeBlock, percentBlock, readyBadge);
        return row;
    }

    private static Border BuildReadyBadge()
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };

        row.Children.Add(new Border
        {
            Width = 16,
            Height = 16,
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(Color.FromArgb(56, 72, 190, 108)),
            BorderBrush = ReadyBrush,
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 4, 0),
            Child = new TextBlock
            {
                Text = "✓",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = ReadyBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        });

        row.Children.Add(new TextBlock
        {
            Text = "Prêt",
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = ReadyBrush,
            VerticalAlignment = VerticalAlignment.Center
        });

        return new Border { Child = row, HorizontalAlignment = HorizontalAlignment.Right };
    }

    private static UIElement BuildDetailRow(CooldownEntry cd, double labelFontSize)
    {
        const double detailHeight = 8;
        var outerRadius = detailHeight / 2;

        var fillColumn = new ColumnDefinition();
        var emptyColumn = new ColumnDefinition();
        var fill = new Border { HorizontalAlignment = HorizontalAlignment.Stretch };

        var detailGrid = new Grid { Height = detailHeight, ClipToBounds = true };
        detailGrid.ColumnDefinitions.Add(fillColumn);
        detailGrid.ColumnDefinitions.Add(emptyColumn);
        detailGrid.Children.Add(fill);
        ApplyBarLayout(cd, fill, fillColumn, emptyColumn, detailGrid);

        var remaining = new TextBlock
        {
            FontSize = labelFontSize,
            Foreground = cd.IsReady ? ReadyBrush : RemainingBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Text = FormatStatusSummary(cd, longForm: true)
        };

        var header = new Grid { Margin = new Thickness(0, 0, 0, 3) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var name = new TextBlock
        {
            Text = CdShortLabel(cd.Type),
            FontSize = labelFontSize,
            Foreground = LabelBrush,
            ToolTip = cd.Type.DisplayName()
        };
        Grid.SetColumn(name, 0);
        Grid.SetColumn(remaining, 1);
        header.Children.Add(name);
        header.Children.Add(remaining);

        var row = new StackPanel { Margin = new Thickness(0, 0, 0, 6) };
        row.Children.Add(header);
        row.Children.Add(new Border
        {
            Background = TrackBrush,
            BorderBrush = TrackEdgeBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(outerRadius),
            Child = detailGrid
        });
        row.Tag = new CooldownRowState(cd, fillColumn, emptyColumn, fill, detailGrid, remaining, null);
        return row;
    }

    private static int GetRemainingPercent(CooldownEntry cd)
    {
        if (cd.IsReady)
            return 0;

        var total = cd.EffectiveDuration.TotalSeconds;
        if (total <= 0)
            return 0;

        var remaining = cd.TimeRemaining?.TotalSeconds ?? 0;
        return (int)Math.Clamp(Math.Round(remaining / total * 100), 0, 100);
    }

    private static int GetElapsedPercent(CooldownEntry cd) =>
        cd.IsReady ? 100 : (int)Math.Clamp(Math.Round(cd.ElapsedFraction * 100), 0, 100);

    private static string FormatStatusSummary(CooldownEntry cd, bool longForm = false)
    {
        if (cd.IsReady)
            return "Prêt";

        var time = FormatTimeRemaining(cd.TimeRemaining, compact: !longForm);
        var pct = GetRemainingPercent(cd);
        return longForm
            ? $"{time} restant ({pct} % · {GetElapsedPercent(cd)} % écoulé)"
            : $"{time} · {pct}%";
    }

    private static SolidColorBrush AccentBrushFor(CooldownType type) => type switch
    {
        CooldownType.Sel_raffine => new SolidColorBrush(Color.FromRgb(210, 185, 90)),
        CooldownType.Mooncloth or CooldownType.Etoffe_lunaire => new SolidColorBrush(Color.FromRgb(175, 130, 220)),
        CooldownType.Arcanite => new SolidColorBrush(Color.FromRgb(110, 165, 230)),
        CooldownType.Transmute_Elementaire => new SolidColorBrush(Color.FromRgb(230, 130, 75)),
        _ => new SolidColorBrush(Color.FromRgb(100, 170, 230))
    };

    private static SolidColorBrush FillBrushFor(CooldownType type) => type switch
    {
        CooldownType.Sel_raffine => new SolidColorBrush(Color.FromRgb(185, 155, 70)),
        CooldownType.Mooncloth or CooldownType.Etoffe_lunaire => new SolidColorBrush(Color.FromRgb(130, 95, 175)),
        CooldownType.Arcanite => new SolidColorBrush(Color.FromRgb(80, 130, 200)),
        CooldownType.Transmute_Elementaire => new SolidColorBrush(Color.FromRgb(200, 105, 60)),
        _ => new SolidColorBrush(Color.FromRgb(70, 140, 220))
    };

    private static string CdRosterLabel(CooldownType type) => type switch
    {
        CooldownType.Arcanite => "Arcanite",
        CooldownType.Transmute_Elementaire => "Élémentaire",
        CooldownType.Mooncloth or CooldownType.Etoffe_lunaire => "Lunaire",
        CooldownType.Sel_raffine => "Sel",
        _ => CdShortLabel(type)
    };

    private static string CdShortLabel(CooldownType type) => type switch
    {
        CooldownType.Arcanite => "Arcanite",
        CooldownType.Transmute_Elementaire => "Élémentaire",
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

    private static string FormatDuration(TimeSpan span, bool compact = false)
    {
        if (span.TotalDays >= 1)
        {
            var days = (int)span.TotalDays;
            var hours = span.Hours;
            return compact
                ? (hours > 0 ? $"{days}j {hours}h" : $"{days}j")
                : $"{days} j {hours:D2} h";
        }

        if (span.TotalHours >= 1)
            return compact
                ? $"{(int)span.TotalHours}h {span.Minutes:D2}m"
                : $"{(int)span.TotalHours} h {span.Minutes:D2} min";

        if (span.TotalSeconds < 60)
            return compact ? $"{span.Seconds}s" : $"{span.Seconds} s";

        return compact
            ? $"{(int)span.TotalMinutes} min"
            : $"{(int)span.TotalMinutes} min";
    }

    private static string FormatTimeRemaining(TimeSpan? ts, bool compact = false)
    {
        if (ts == null)
            return "—";

        return FormatDuration(ts.Value, compact);
    }
}
