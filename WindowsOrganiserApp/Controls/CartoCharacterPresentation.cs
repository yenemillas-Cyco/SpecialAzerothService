using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using SpecialAzerothService.Core.Models.Carto;
using SpecialAzerothService.Core.Models.WowSync;
using SpecialAzerothService.Core.Services;
using CartoProfessionCooldownsService = SpecialAzerothService.Core.Services.CartoProfessionCooldowns;
using WindowsOrganiserApp.ViewModels;

namespace WindowsOrganiserApp.Controls;

/// <summary>En-têtes et puces partagés entre vignettes roster et popup personnage.</summary>
public static class CartoCharacterPresentation
{
    /// <summary>Propriétaire « Moi » — distinct des catégories dorées en dessous.</summary>
    public static readonly Brush MoiUserBrush = new SolidColorBrush(Color.FromRgb(168, 198, 228));
    public static readonly Brush DefaultUserBrush = new SolidColorBrush(Color.FromRgb(220, 200, 160));
    public static readonly Brush SyncDateBrush = new SolidColorBrush(Color.FromRgb(130, 125, 110));
    public static readonly Brush ZoneBrush = new SolidColorBrush(Color.FromRgb(140, 200, 170));
    public static readonly Brush NoteBrush = new SolidColorBrush(Color.FromRgb(200, 195, 175));
    public static readonly Brush DimBrush = new SolidColorBrush(Color.FromRgb(150, 142, 125));
    public static bool IsPersonnagesCategory(CharacterStatus status) =>
        status is CharacterStatus.Main or CharacterStatus.Reroll;

    public static bool IsPersonnages(WowCharacter ch) =>
        ch.Status is CharacterStatus.Main or CharacterStatus.Reroll;

    public static bool IsBank(WowCharacter ch) => ch.Status == CharacterStatus.Banque;

    public static bool IsMinimalBody(WowCharacter ch) =>
        ch.Status is CharacterStatus.TpBoy or CharacterStatus.ClicBoys;

    public static bool ShowQuestBody(WowCharacter ch) => IsPersonnages(ch);

    public static bool ShowProfessionsBody(WowCharacter ch) => IsPersonnages(ch);

    public static bool ShowCooldownsBody(WowCharacter ch) => IsPersonnages(ch);

    /// <summary>Métier CD éligible (≥ 250) ou cooldown déjà actif/suivi.</summary>
    public static bool HasTrackedProfession(WowCharacter ch, WowCharacterData? sync = null) =>
        CartoProfessionCooldowns.QualifiesForCooldownRoster(ch, sync);

    /// <summary>Or du personnage (toutes catégories locales, dont Banque).</summary>
    public static bool ShowGoldBody(WowCharacter ch) => true;

    /// <summary>Inventaire + banque WowSync (personnages principaux et persos Banque).</summary>
    public static bool ShowInventoryBankSection(WowCharacter ch) =>
        IsPersonnages(ch) || IsBank(ch);

    public static Brush GetUserHeaderBrush(CartoUser user, CartoViewModel vm)
    {
        if (user.Name.Equals(CartoUserMigration.DefaultUserName, StringComparison.OrdinalIgnoreCase))
            return MoiUserBrush;

        return DefaultUserBrush;
    }

    public static Brush GetCharacterNameBrush(WowCharacter ch, CartoViewModel vm)
    {
        var classColor = (Color)ColorConverter.ConvertFromString(WowClassColors.GetHexColor(ch.Class));
        return new SolidColorBrush(classColor);
    }

    public static SolidColorBrush GetClassBrush(WowClass cls)
    {
        var classColor = (Color)ColorConverter.ConvertFromString(WowClassColors.GetHexColor(cls));
        return new SolidColorBrush(classColor);
    }

    public static SolidColorBrush GetFactionBrush(Faction faction) => faction switch
    {
        Faction.Horde => new SolidColorBrush(Color.FromRgb(204, 51, 51)),
        Faction.Alliance => new SolidColorBrush(Color.FromRgb(74, 158, 255)),
        _ => new SolidColorBrush(Color.FromRgb(160, 160, 160))
    };

    public sealed class CharacterHeaderOptions
    {
        public double NameFontSize { get; init; } = 14;
        public double LevelFontSize { get; init; } = 12;
        public double MetaFontSize { get; init; } = 9;
        public int IconWidth { get; init; } = 40;
        public bool ShowZone { get; init; } = true;
        public bool ShowSyncDate { get; init; }
        public bool ShowQuestIcons { get; init; }
        public int QuestIconSize { get; init; } = 22;
        public bool ShowAccountName { get; init; } = true;
        public bool ShowCooldownBarsOnPortrait { get; init; }
    }

    /// <summary>En-tête : icône classe, nom/niveau/compte, têtes/cœurs, actions, sync+zone.</summary>
    public static Grid BuildCharacterHeaderGrid(
        WowCharacter ch,
        CartoViewModel vm,
        CharacterHeaderOptions? options = null,
        UIElement? actionsContent = null,
        WowCharacterData? sync = null)
    {
        options ??= new CharacterHeaderOptions();
        var nameBrush = GetCharacterNameBrush(ch, vm);
        var accountName = vm.GetCharacterAccountDisplayName(ch);
        var showAccount = !string.IsNullOrEmpty(accountName);

        UIElement? questContent = null;
        if (options.ShowQuestIcons && ShowQuestBody(ch))
        {
            var questRow = BuildQuestIconRow(ch, sync, options.QuestIconSize, horizontal: true);
            if (questRow.Children.Count > 0)
                questContent = questRow;
        }

        var hasQuest = questContent != null;
        var hasActions = actionsContent != null;

        var grid = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var questCol = -1;
        var actionsCol = -1;
        if (hasQuest)
        {
            questCol = grid.ColumnDefinitions.Count;
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        }

        if (hasActions)
        {
            actionsCol = grid.ColumnDefinitions.Count;
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        }

        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var metaLine = BuildSyncZoneLine(ch, vm, options);
        if (metaLine != null)
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var rowSpan = metaLine != null ? 2 : 1;

        var iconColumn = BuildClassIconColumn(ch, options.IconWidth, options.ShowCooldownBarsOnPortrait, sync);
        Grid.SetRow(iconColumn, 0);
        Grid.SetColumn(iconColumn, 0);
        Grid.SetRowSpan(iconColumn, rowSpan);
        grid.Children.Add(iconColumn);

        var nameLine = new TextBlock
        {
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };
        nameLine.Inlines.Add(new Run(ch.Name)
        {
            FontWeight = FontWeights.Bold,
            Foreground = nameBrush,
            FontSize = options.NameFontSize
        });
        nameLine.Inlines.Add(new Run($"  {ch.Level}")
        {
            FontSize = options.LevelFontSize,
            Foreground = Brushes.White
        });
        if (showAccount && options.ShowAccountName)
        {
            nameLine.Inlines.Add(new Run($"  ·  {accountName}")
            {
                FontSize = options.LevelFontSize - 1,
                Foreground = DimBrush
            });
        }

        AppendXpToNameLine(nameLine, vm.GetCharacterXpPercent(ch), ch.Level);
        Grid.SetRow(nameLine, 0);
        Grid.SetColumn(nameLine, 1);
        grid.Children.Add(nameLine);

        if (metaLine != null)
        {
            Grid.SetRow(metaLine, 1);
            Grid.SetColumn(metaLine, 1);
            grid.Children.Add(metaLine);
        }

        if (hasQuest)
        {
            Grid.SetRow(questContent!, 0);
            Grid.SetColumn(questContent!, questCol);
            Grid.SetRowSpan(questContent!, rowSpan);
            questContent!.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            questContent.SetValue(FrameworkElement.MarginProperty, new Thickness(8, 0, hasActions ? 6 : 0, 0));
            grid.Children.Add(questContent);
        }

        if (hasActions)
        {
            Grid.SetRow(actionsContent!, 0);
            Grid.SetColumn(actionsContent!, actionsCol);
            Grid.SetRowSpan(actionsContent!, rowSpan);
            actionsContent!.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            actionsContent.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Right);
            actionsContent.SetValue(FrameworkElement.MarginProperty, new Thickness(4, 0, 0, 0));
            grid.Children.Add(actionsContent);
        }

        return grid;
    }

    /// <summary>Faction (cadre couleur faction) + classe (cadre couleur classe), barres CD optionnelles.</summary>
    public static UIElement BuildPortraitIcons(
        WowCharacter ch,
        int classSize,
        Thickness? classMargin = null,
        bool showCooldownBars = false,
        WowCharacterData? sync = null)
    {
        var iconsRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };

        var frameSize = classSize + 4;

        var factionKind = WowFactionIcon.ResolveFaction(ch.Race);
        if (factionKind != null)
        {
            var fIcon = WowFactionIcon.Create(factionKind.Value, classSize);
            if (fIcon != null)
            {
                iconsRow.Children.Add(new Border
                {
                    Width = frameSize,
                    MinWidth = frameSize,
                    MinHeight = frameSize,
                    Margin = new Thickness(0, 0, 4, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    BorderBrush = GetFactionBrush(factionKind.Value),
                    BorderThickness = new Thickness(1.5),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(2, 3, 2, 3),
                    Background = Brushes.Transparent,
                    Child = fIcon
                });
            }
        }

        var classImage = WowClassIcon.Create(ch.Class, classSize);
        classImage.Stretch = Stretch.Uniform;
        classImage.HorizontalAlignment = HorizontalAlignment.Center;
        classImage.VerticalAlignment = VerticalAlignment.Center;
        classImage.MinHeight = classSize;
        classImage.MaxHeight = 80;
        RenderOptions.SetBitmapScalingMode(classImage, BitmapScalingMode.HighQuality);

        iconsRow.Children.Add(new Border
        {
            Width = frameSize,
            MinWidth = frameSize,
            MinHeight = frameSize,
            VerticalAlignment = VerticalAlignment.Center,
            BorderBrush = GetClassBrush(ch.Class),
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(2, 3, 2, 3),
            Background = Brushes.Transparent,
            Child = classImage
        });

        var outer = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = classMargin ?? new Thickness(0, 0, 8, 0)
        };
        outer.Children.Add(iconsRow);

        return outer;
    }

    private static UIElement BuildClassIconColumn(
        WowCharacter ch,
        int width,
        bool showCooldownBars,
        WowCharacterData? sync = null) =>
        BuildPortraitIcons(ch, width, showCooldownBars: showCooldownBars, sync: sync);

    private static StackPanel? BuildSyncZoneLine(
        WowCharacter ch,
        CartoViewModel vm,
        CharacterHeaderOptions options)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 3, 0, 0)
        };

        if (options.ShowSyncDate)
        {
            var syncLabel = BuildSyncDateLabel(vm.GetCharacterSyncLabel(ch), options.MetaFontSize);
            if (syncLabel != null)
                row.Children.Add(syncLabel);
        }

        if (options.ShowZone)
        {
            var zoneLabel = BuildZoneLabel(vm.GetCharacterZoneLabel(ch), options.MetaFontSize + 1, maxWidth: 130);
            if (zoneLabel != null)
            {
                zoneLabel.Margin = new Thickness(8, 0, 0, 0);
                row.Children.Add(zoneLabel);
            }
        }

        return row.Children.Count > 0 ? row : null;
    }

    public static UIElement BuildClassIcon(WowClass wowClass, int size = 28)
    {
        var icon = WowClassIcon.Create(wowClass, size);
        var frame = new Border
        {
            Width = size + 4,
            Height = size + 4,
            CornerRadius = new CornerRadius(4),
            BorderBrush = GetClassBrush(wowClass),
            BorderThickness = new Thickness(1.5),
            Padding = new Thickness(1),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = icon
        };
        return frame;
    }

    public static string? FormatSyncDateDisplay(string? lastUpdate)
    {
        if (string.IsNullOrWhiteSpace(lastUpdate))
            return null;

        var raw = lastUpdate.Trim();
        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var utc)
            || DateTime.TryParse(raw, out utc))
        {
            return utc.ToString("dddd d MMMM yyyy · HH:mm", CultureInfo.GetCultureInfo("fr-FR"));
        }

        return raw;
    }

    public static TextBlock? BuildSyncDateLabel(string? lastUpdate, double fontSize = 9)
    {
        var formatted = FormatSyncDateDisplay(lastUpdate);
        if (string.IsNullOrWhiteSpace(formatted))
            return null;

        return new TextBlock
        {
            Text = $"Sync {formatted}",
            FontSize = fontSize,
            Foreground = SyncDateBrush,
            TextTrimming = TextTrimming.CharacterEllipsis,
            ToolTip = formatted
        };
    }

    public static Border? BuildProminentSyncBanner(string? lastUpdate)
    {
        var formatted = FormatSyncDateDisplay(lastUpdate);
        if (string.IsNullOrWhiteSpace(formatted))
            return null;

        var panel = new StackPanel();
        panel.Children.Add(new TextBlock
        {
            Text = "Dernière mise à jour addon (fichier local)",
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = DimBrush,
            Margin = new Thickness(0, 0, 0, 4)
        });
        panel.Children.Add(new TextBlock
        {
            Text = formatted,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(150, 215, 255)),
            TextWrapping = TextWrapping.Wrap,
            ToolTip = lastUpdate?.Trim()
        });

        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(48, 40, 90, 140)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(120, 80, 150, 210)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 0, 0, 10),
            Child = panel
        };
    }

    public static TextBlock? BuildZoneLabel(string? zoneDisplay, double fontSize = 10, double maxWidth = 0)
    {
        if (string.IsNullOrWhiteSpace(zoneDisplay))
            return null;

        var block = new TextBlock
        {
            Text = zoneDisplay.Trim(),
            FontSize = fontSize,
            Foreground = ZoneBrush,
            TextTrimming = TextTrimming.CharacterEllipsis,
            ToolTip = zoneDisplay.Trim()
        };

        if (maxWidth > 0)
            block.MaxWidth = maxWidth;

        return block;
    }

    public static TextBlock? BuildXpLabel(double xpPercent, int level, double fontSize = 10)
    {
        if (level >= 60 || xpPercent < 0)
            return null;

        return new TextBlock
        {
            Text = $"XP {xpPercent:F1} %",
            FontSize = fontSize,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(180, 220, 255)),
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    public static TextBlock? BuildNotePreview(string? note, int maxLen = 80, double fontSize = 10)
    {
        if (string.IsNullOrWhiteSpace(note))
            return null;

        var text = note.Trim();
        if (text.Length > maxLen)
            text = text[..(maxLen - 1)] + "…";

        return new TextBlock
        {
            Text = text,
            FontSize = fontSize,
            Foreground = NoteBrush,
            TextTrimming = TextTrimming.CharacterEllipsis,
            ToolTip = note.Trim()
        };
    }

    public static UIElement BuildQuestIconChip(
        QuestItemType type,
        WowCharacterData? sync,
        bool hasItem,
        bool planned,
        int iconSize = 24)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };

        var item = CartoCharacterEnricher.ResolveQuestIconItem(sync, type);
        if (item.ItemId > 0)
            row.Children.Add(CartoMapQuestIcon.Create(item, iconSize));

        if (planned && !hasItem)
        {
            row.Children.Add(new TextBlock
            {
                Text = "Prévu",
                FontSize = 9,
                Foreground = new SolidColorBrush(Color.FromRgb(255, 200, 80)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(3, 0, 0, 0)
            });
        }

        return row;
    }

    public static Panel BuildQuestIconRow(
        WowCharacter ch,
        WowCharacterData? sync,
        int iconSize = 22,
        bool horizontal = false)
    {
        Panel panel = horizontal
            ? new StackPanel { Orientation = Orientation.Horizontal }
            : new WrapPanel();

        foreach (var type in Enum.GetValues<QuestItemType>())
        {
            var entry = ch.QuestItems.FirstOrDefault(q => q.Type == type);
            var hasItem = entry?.HasItem == true;
            var planned = entry?.PlannedTurnIn != null;
            if (!hasItem && !planned)
                continue;

            panel.Children.Add(BuildQuestIconChip(type, sync, hasItem, planned, iconSize));
        }

        return panel;
    }

    public static string FormatProfession(ProfessionType type) => type switch
    {
        ProfessionType.Travail_du_cuir => "Travail du cuir",
        ProfessionType.Exploitation_miniere => "Exploitation minière",
        ProfessionType.Depecage => "Dépeçage",
        _ => type.ToString().Replace('_', ' ')
    };

    public static UIElement? BuildCooldownsSummary(WowCharacter ch, double fontSize = 10, WowCharacterData? sync = null) =>
        CartoCooldownDisplay.BuildPanel(ch, fontSize, sync);

    public static TextBlock BuildNameLevelLine(WowCharacter ch, Brush nameBrush, double nameSize = 14, double levelSize = 12)
    {
        var line = new TextBlock
        {
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        line.Inlines.Add(new Run(ch.Name)
        {
            FontWeight = FontWeights.Bold,
            Foreground = nameBrush,
            FontSize = nameSize
        });
        line.Inlines.Add(new Run($"  {ch.Level}")
        {
            FontSize = levelSize,
            Foreground = Brushes.White
        });

        var xp = ch.Level < 60 ? null : (double?)null; // filled by caller via sync
        return line;
    }

    public static void AppendXpToNameLine(TextBlock line, double? xpPercent, int level)
    {
        if (level >= 60 || xpPercent is not >= 0)
            return;

        line.Inlines.Add(new Run($"  ·  XP {xpPercent:F1} %")
        {
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.FromRgb(180, 220, 255))
        });
    }

    private static string FormatTimeRemaining(TimeSpan? ts)
    {
        if (ts == null) return "—";
        if (ts.Value.TotalHours >= 1)
            return $"{(int)ts.Value.TotalHours} h {ts.Value.Minutes:D2} min";
        if (ts.Value.TotalSeconds < 60)
            return $"{ts.Value.Seconds} s";
        return $"{(int)ts.Value.TotalMinutes} min {ts.Value.Seconds:D2} s";
    }
}
