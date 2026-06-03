using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SpecialAzerothService.Core.Models.Carto;
using SpecialAzerothService.Core.Models.WowSync;
using SpecialAzerothService.Core.Services;
using WindowsOrganiserApp.ViewModels;

namespace WindowsOrganiserApp.Controls;

public sealed class CartoDockCardOptions
{
    public bool CooldownRosterOnly { get; init; }
}

public sealed class CartoDockCardCallbacks
{
    public Action<WowCharacter>? ToggleMapVisibility { get; init; }
    public Action<WowCharacter>? OpenDetails { get; init; }
    public Action<WowCharacter, Border, MouseButtonEventArgs>? DragStart { get; init; }
    public Action<WowCharacter, Border, MouseEventArgs>? DragMove { get; init; }
    public Action<WowCharacter, Border, MouseButtonEventArgs>? DragEnd { get; init; }
}

/// <summary>Carte compacte dans le bandeau latéral Carto.</summary>
public static class CartoCharacterDockCard
{
    private static readonly Brush RowBg = new SolidColorBrush(Color.FromArgb(40, 18, 14, 8));
    private static readonly Brush ProfText = new SolidColorBrush(Color.FromRgb(190, 175, 130));

    private const double RowGap = 4;

    public static Border Build(
        WowCharacter ch,
        CartoViewModel vm,
        CartoDockCardCallbacks? callbacks = null,
        CartoDockCardOptions? options = null,
        UIElement? headerActions = null)
    {
        options ??= new CartoDockCardOptions();
        vm.ApplySyncEnrichment(ch);
        var classBrush = CartoCharacterPresentation.GetClassBrush(ch.Class);
        var sync = vm.FindWowSyncCharacter(ch);

        var root = new StackPanel();

        var header = CartoRosterPanelUi.StretchWidth(CartoCharacterPresentation.BuildCharacterHeaderGrid(
            ch,
            vm,
            new CartoCharacterPresentation.CharacterHeaderOptions
            {
                NameFontSize = 14,
                LevelFontSize = 12,
                IconWidth = 40,
                ShowZone = false,
                ShowSyncDate = false,
                ShowCooldownBarsOnPortrait = false,
                ShowQuestIcons = !options.CooldownRosterOnly && CartoCharacterPresentation.ShowQuestBody(ch),
                QuestIconSize = 22
            },
            headerActions,
            sync));
        header.Margin = new Thickness(0, 0, 0, options.CooldownRosterOnly ? 2 : 4);
        root.Children.Add(header);

        if (CartoCooldownDisplay.HasDisplayableCooldowns(ch, sync))
        {
            var cdStrip = CartoCooldownDisplay.BuildRosterCardStrip(ch, sync);
            if (cdStrip is FrameworkElement cdFe)
                root.Children.Add(CartoRosterPanelUi.StretchWidth(cdFe));
        }

        if (options.CooldownRosterOnly)
        {
            return WrapCard(root, classBrush);
        }

        var body = new StackPanel();

        if (CartoCharacterPresentation.ShowGoldBody(ch) && sync is { Gold: > 0 })
        {
            var goldLine = new WrapPanel { Margin = new Thickness(0, 0, 0, RowGap) };
            goldLine.Children.Add(WowCurrencyDisplay.Build(sync.Gold, iconSize: 16, fontSize: 12));
            body.Children.Add(goldLine);
        }

        if (CartoCharacterPresentation.ShowProfessionsBody(ch))
        {
            var infoLine = new WrapPanel { Margin = new Thickness(0, 0, 0, RowGap) };

            if (ch.Class == WowClass.Demoniste && ch.ShardCount > 0)
            {
                var shardItem = new WowItem { ItemId = 6265, Name = "Fragment d'âme", Count = ch.ShardCount, Quality = 1 };
                infoLine.Children.Add(BuildInlineChip(
                    CartoMapQuestIcon.Create(shardItem, 22),
                    $"{ch.ShardCount}",
                    new SolidColorBrush(Color.FromRgb(148, 130, 201))));
            }

            var profLine = BuildProfessionsLine(ch, sync);
            if (profLine != null)
                infoLine.Children.Add(profLine);

            if (infoLine.Children.Count > 0)
                body.Children.Add(infoLine);
        }
        else if (ch.Class == WowClass.Demoniste && ch.ShardCount > 0)
        {
            var shardItem = new WowItem { ItemId = 6265, Name = "Fragment d'âme", Count = ch.ShardCount, Quality = 1 };
            body.Children.Add(BuildInlineChip(
                CartoMapQuestIcon.Create(shardItem, 22),
                $"{ch.ShardCount}",
                new SolidColorBrush(Color.FromRgb(148, 130, 201))));
        }

        if (body.Children.Count > 0)
            root.Children.Add(body);

        return WrapCard(root, classBrush);
    }

    private static Border WrapCard(StackPanel root, Brush classBrush) =>
        CartoRosterPanelUi.StretchWidth(new Border
        {
            Background = RowBg,
            BorderBrush = classBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 7, 8, 7),
            Margin = new Thickness(0, 0, 0, 4),
            Child = CartoRosterPanelUi.StretchWidth(root),
            Cursor = Cursors.Hand,
            ToolTip = "Glisser vers la carte ou un autre cadre · clic pour le détail"
        });

    private static Border BuildInlineChip(UIElement icon, string text, Brush textBrush)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        };
        row.Children.Add(icon);
        row.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = textBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 0, 0)
        });
        return new Border { Child = row };
    }

    private static TextBlock? BuildProfessionsLine(WowCharacter ch, WowCharacterData? sync)
    {
        var parts = new List<string>();

        if (ch.Professions.Count > 0)
        {
            foreach (var prof in ch.Professions)
            {
                var syncProf = sync?.Professions.FirstOrDefault(p =>
                    p.Name.Contains(FormatProfession(prof.Type), StringComparison.OrdinalIgnoreCase)
                    || FormatProfession(prof.Type).Contains(p.Name, StringComparison.OrdinalIgnoreCase));
                var max = syncProf?.MaxRank > 0 ? syncProf.MaxRank : 300;
                parts.Add($"{FormatProfession(prof.Type)} {prof.Skill}/{max}");
            }
        }
        else if (sync is { Professions.Count: > 0 })
        {
            foreach (var prof in sync.Professions)
                parts.Add($"{prof.Name} {prof.Rank}/{prof.MaxRank}");
        }

        if (parts.Count == 0)
            return null;

        return new TextBlock
        {
            Text = string.Join("   ·   ", parts),
            FontSize = 10,
            Foreground = ProfText,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    public static string FormatClassName(WowClass c) => c switch
    {
        WowClass.Guerrier => "Guerrier",
        WowClass.Paladin => "Paladin",
        WowClass.Chasseur => "Chasseur",
        WowClass.Voleur => "Voleur",
        WowClass.Pretre => "Prêtre",
        WowClass.Chaman => "Chaman",
        WowClass.Mage => "Mage",
        WowClass.Demoniste => "Démoniste",
        WowClass.Druide => "Druide",
        _ => c.ToString()
    };

    public static string FormatProfession(ProfessionType type) =>
        CartoCharacterPresentation.FormatProfession(type);

    public static string FormatQuestItem(QuestItemType type) => type switch
    {
        QuestItemType.Tete_de_Rend => "Tête de Rend",
        QuestItemType.Tete_dOnyxia => "Tête d'Onyxia",
        QuestItemType.Tete_de_Nefarian => "Tête de Nefarian",
        QuestItemType.Coeur_de_Hakkar => "Cœur de Hakkar",
        _ => type.ToString()
    };
}
