using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using SpecialAzerothService.Core.Models.Bounty;
using WindowsOrganiserApp.Controls;
using WindowsOrganiserApp.ViewModels;

namespace WindowsOrganiserApp.Views;

public partial class BountyView : UserControl
{
    private static readonly Dictionary<string, string> ClassColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Guerrier"] = "#C79C6E",
        ["Paladin"] = "#F58CBA",
        ["Chasseur"] = "#ABD473",
        ["Voleur"] = "#FFF569",
        ["Prêtre"] = "#FFFFFF",
        ["Pretre"] = "#FFFFFF",
        ["Chaman"] = "#0070DE",
        ["Mage"] = "#69CCF0",
        ["Démoniste"] = "#9482C9",
        ["Demoniste"] = "#9482C9",
        ["Druide"] = "#FF7D0A"
    };

    public BountyView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (BountyGrid.Items.Count > 0)
                BountyGrid.SelectedIndex = 0;
        };
    }

    private BountyViewModel? Vm => DataContext as BountyViewModel;

    private void ToggleRules_Click(object sender, RoutedEventArgs e)
    {
        PopupRules.IsOpen = !PopupRules.IsOpen;
    }

    private void CloseRules_Click(object sender, RoutedEventArgs e)
    {
        PopupRules.IsOpen = false;
        Vm?.SaveRulesCommand.Execute(null);
    }

    private void BountyGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (BountyGrid.SelectedItem is BountyEntry bounty)
        {
            Vm?.EditBountyCommand.Execute(bounty);
            e.Handled = true;
        }
    }

    private void ToggleAllExport_Click(object sender, RoutedEventArgs e)
    {
        Vm?.ToggleAllExportCommand.Execute(null);
    }

    private void ExportCheckBox_Click(object sender, RoutedEventArgs e)
    {
        Vm?.RefreshAfterExportToggle();
    }

    private void ScreenshotBounty_Click(object sender, RoutedEventArgs e)
    {
        BountyEntry? bounty = null;
        if (sender is FrameworkElement fe && fe.DataContext is BountyEntry b)
            bounty = b;
        if (bounty == null && BountyGrid.SelectedItem is BountyEntry sel)
            bounty = sel;
        if (bounty == null)
        {
            MessageBox.Show("Selectionne une prime d'abord.", "Avis de Recherche", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        RenderAndCopyPoster(bounty);
    }

    private void ScreenshotSelected_Click(object sender, RoutedEventArgs e)
    {
        if (Vm == null || Vm.Bounties.Count == 0)
        {
            MessageBox.Show("Aucune prime a afficher.", "Avis de Recherche", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var active = Vm.Bounties
            .Where(b => !b.IsCompleted && b.IsSelectedForExport)
            .OrderByDescending(b => b.TotalGold)
            .ThenBy(b => b.TargetName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (active.Count == 0)
            active = Vm.Bounties.Where(b => !b.IsCompleted).OrderByDescending(b => b.TotalGold).ToList();
        RenderAndCopyFullPoster(Vm.Rules, active);
    }

    private static void RenderAndCopyFullPoster(string rules, List<BountyEntry> bounties)
    {
        try
        {
            BountyPosterRewardDisplay.WarmupIcons();
            var poster = BuildFullWantedPoster(rules, bounties);

            var desiredWidth = 900.0;
            poster.Measure(new Size(desiredWidth, double.PositiveInfinity));
            var finalHeight = Math.Max(poster.DesiredSize.Height, 400);
            poster.Arrange(new Rect(0, 0, desiredWidth, finalHeight));
            poster.UpdateLayout();

            var scale = 2;
            var pixelW = (int)(desiredWidth * scale);
            var pixelH = (int)(finalHeight * scale);
            var dpi = 96 * scale;

            var rtb = new RenderTargetBitmap(pixelW, pixelH, dpi, dpi, PixelFormats.Pbgra32);
            rtb.Render(poster);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));

            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Primes");
            Directory.CreateDirectory(dir);
            var filePath = Path.Combine(dir, $"Primes_{DateTime.Now:yyyyMMdd_HHmmss}.png");

            using (var fs = new FileStream(filePath, FileMode.Create))
                encoder.Save(fs);

            try { Clipboard.SetImage(rtb); } catch { }

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(filePath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erreur generation avis :\n{ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static Border BuildFullWantedPoster(string rules, List<BountyEntry> bounties)
    {
        var darkGold = BrushFromHex("#D4A017");
        var parchment = BrushFromHex("#D4C4A0");
        var brown = BrushFromHex("#806030");

        var stack = new StackPanel();

        stack.Children.Add(MakeOrnament(brown));

        var title = new TextBlock
        {
            Text = "AVIS DE RECHERCHE — MORTS OU VIFS",
            FontSize = 28, FontWeight = FontWeights.Bold, FontFamily = new FontFamily("Georgia"),
            Foreground = darkGold, HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 6),
            Effect = new DropShadowEffect { BlurRadius = 12, ShadowDepth = 0, Opacity = 0.7, Color = Colors.Gold }
        };
        stack.Children.Add(title);
        stack.Children.Add(new TextBlock
        {
            Text = "CLASSEMENT DES PRIMES — DE LA PLUS CHÈRE À LA MOINS CHÈRE",
            FontSize = 11, FontWeight = FontWeights.SemiBold, FontFamily = new FontFamily("Georgia"),
            Foreground = parchment, HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 10)
        });

        stack.Children.Add(MakeSeparator("#806030"));

        if (!string.IsNullOrWhiteSpace(rules))
        {
            stack.Children.Add(new TextBlock
            {
                Text = "RÈGLEMENT",
                FontSize = 12, FontWeight = FontWeights.Bold, FontFamily = new FontFamily("Georgia"),
                Foreground = darkGold, HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 4)
            });
            foreach (var line in rules.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                stack.Children.Add(new TextBlock
                {
                    Text = line.Trim(),
                    FontSize = 10, FontStyle = FontStyles.Italic, FontFamily = new FontFamily("Georgia"),
                    Foreground = parchment, HorizontalAlignment = HorizontalAlignment.Center,
                    TextWrapping = TextWrapping.Wrap, Margin = new Thickness(30, 0, 30, 1)
                });
            }
            stack.Children.Add(MakeSeparator("#D4A017", 10));
        }

        if (bounties.Count > 0)
        {
            stack.Children.Add(BuildPodiumSection(bounties.Take(3).ToList()));
            stack.Children.Add(MakeSeparator("#D4A017", 12));
        }

        if (bounties.Count > 3)
        {
            stack.Children.Add(new TextBlock
            {
                Text = "SUITE DU CLASSEMENT",
                FontSize = 11, FontWeight = FontWeights.SemiBold, FontFamily = new FontFamily("Georgia"),
                Foreground = darkGold, HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 6)
            });

            var headerGrid = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            AddHeaderCell(headerGrid, "#", 0, darkGold);
            AddHeaderCell(headerGrid, "CIBLE", 1, darkGold);
            AddHeaderCell(headerGrid, "PRIME", 2, darkGold);
            AddHeaderCell(headerGrid, "MOTIF", 3, darkGold);
            AddHeaderCell(headerGrid, "COMMANDITAIRES", 4, darkGold);
            stack.Children.Add(headerGrid);

            stack.Children.Add(new Border
            {
                Height = 1, Margin = new Thickness(10, 0, 10, 4),
                Background = BrushFromHex("#50806030")
            });

            for (var i = 3; i < bounties.Count; i++)
                stack.Children.Add(BuildRankedPosterRow(i + 1, bounties[i]));
        }

        stack.Children.Add(MakeSeparator("#D4A017", 10));
        var total = bounties.Sum(b => b.TotalGold);
        var totalRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 8)
        };
        totalRow.Children.Add(new TextBlock
        {
            Text = "TOTAL DES PRIMES : ",
            FontSize = 20, FontWeight = FontWeights.Bold, FontFamily = new FontFamily("Georgia"),
            Foreground = BrushFromHex("#FFD700"),
            VerticalAlignment = VerticalAlignment.Center,
            Effect = new DropShadowEffect { BlurRadius = 10, ShadowDepth = 0, Opacity = 0.5, Color = Colors.Gold }
        });
        totalRow.Children.Add(BountyPosterRewardDisplay.Build(total, fontSize: 20, iconSize: 22));
        stack.Children.Add(totalRow);

        stack.Children.Add(MakeOrnament(brown));

        return new Border
        {
            Width = 920,
            Padding = new Thickness(30, 22, 30, 22),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(3),
            BorderBrush = BrushFromHex("#8B7355"),
            Effect = new DropShadowEffect { BlurRadius = 24, ShadowDepth = 4, Opacity = 0.55, Color = Colors.Black },
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0.5, 0),
                EndPoint = new Point(0.5, 1),
                GradientStops =
                [
                    new GradientStop((Color)ColorConverter.ConvertFromString("#3A2A14")!, 0),
                    new GradientStop((Color)ColorConverter.ConvertFromString("#2A1E0E")!, 0.3),
                    new GradientStop((Color)ColorConverter.ConvertFromString("#1A120A")!, 0.7),
                    new GradientStop((Color)ColorConverter.ConvertFromString("#2A1E0E")!, 1)
                ]
            },
            Child = stack
        };
    }

    private static UIElement BuildPodiumSection(List<BountyEntry> top)
    {
        var outer = new StackPanel { Margin = new Thickness(0, 12, 0, 8) };
        outer.Children.Add(new TextBlock
        {
            Text = "P O D I U M",
            FontSize = 14, FontWeight = FontWeights.Bold, FontFamily = new FontFamily("Georgia"),
            Foreground = BrushFromHex("#D4A017"), HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 16),
            Effect = new DropShadowEffect { BlurRadius = 10, ShadowDepth = 0, Opacity = 0.5, Color = Colors.Gold }
        });

        var stage = new Grid { Margin = new Thickness(24, 0, 24, 0) };
        stage.ColumnDefinitions.Add(new ColumnDefinition());
        stage.ColumnDefinitions.Add(new ColumnDefinition());
        stage.ColumnDefinitions.Add(new ColumnDefinition());
        stage.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        stage.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });

        // Disposition classique : 2e | 1er | 3e
        if (top.Count > 1)
        {
            var col = BuildPodiumColumn(top[1], 2, pedestalHeight: 78, "#8E8E8E", "#4A4A4A", "#C8C8C8");
            Grid.SetColumn(col, 0);
            stage.Children.Add(col);
        }

        if (top.Count > 0)
        {
            var col = BuildPodiumColumn(top[0], 1, pedestalHeight: 108, "#C9A227", "#6B4A10", "#FFD700");
            Grid.SetColumn(col, 1);
            stage.Children.Add(col);
        }

        if (top.Count > 2)
        {
            var col = BuildPodiumColumn(top[2], 3, pedestalHeight: 58, "#A0622A", "#5C3818", "#CD7F32");
            Grid.SetColumn(col, 2);
            stage.Children.Add(col);
        }

        var basePlinth = new Border
        {
            Height = 10,
            CornerRadius = new CornerRadius(0, 0, 4, 4),
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops =
                [
                    new GradientStop((Color)ColorConverter.ConvertFromString("#5A4020")!, 0),
                    new GradientStop((Color)ColorConverter.ConvertFromString("#2A1E0E")!, 1)
                ]
            },
            BorderBrush = BrushFromHex("#806030", 0.5),
            BorderThickness = new Thickness(1, 0, 1, 1)
        };
        Grid.SetRow(basePlinth, 1);
        Grid.SetColumnSpan(basePlinth, 3);
        stage.Children.Add(basePlinth);

        outer.Children.Add(stage);
        return outer;
    }

    private static UIElement BuildPodiumColumn(
        BountyEntry bounty, int rank, double pedestalHeight,
        string pedestalDark, string pedestalDarker, string pedestalLight)
    {
        var isFirst = rank == 1;
        var portraitSize = isFirst ? 104.0 : 84.0;

        var column = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Bottom,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(isFirst ? 6 : 12, 0, isFirst ? 6 : 12, 0)
        };

        column.Children.Add(BountyElitePortrait.Build(bounty, rank, portraitSize));

        var nameFontSize = rank switch { 1 => 24, 2 => 18, _ => 15 };

        column.Children.Add(new TextBlock
        {
            Text = bounty.TargetName,
            FontSize = nameFontSize,
            FontWeight = FontWeights.Bold,
            FontFamily = new FontFamily("Georgia"),
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = isFirst ? 230 : 180,
            Margin = new Thickness(0, -2, 0, 0),
            Foreground = GetClassBrush(bounty.TargetClass),
            Effect = new DropShadowEffect { BlurRadius = 6, ShadowDepth = 0, Opacity = 0.45, Color = Colors.Black }
        });

        var rewardHost = BountyPosterRewardDisplay.Build(bounty, isFirst ? 26 : 20, isFirst ? 24 : 18);
        if (rewardHost is FrameworkElement fe)
            fe.Margin = new Thickness(0, 0, 0, 0);
        column.Children.Add(rewardHost);

        if (!string.IsNullOrWhiteSpace(bounty.Reason))
        {
            column.Children.Add(new TextBlock
            {
                Text = bounty.Reason,
                FontSize = 9,
                FontStyle = FontStyles.Italic,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = isFirst ? 220 : 175,
                Foreground = BrushFromHex("#A09070"),
                Margin = new Thickness(0, 4, 0, 0)
            });
        }

        if (bounty.Contributors.Count > 0)
        {
            column.Children.Add(new TextBlock
            {
                Text = "Commanditaires",
                FontSize = 8,
                FontWeight = FontWeights.SemiBold,
                FontFamily = new FontFamily("Georgia"),
                Foreground = BrushFromHex("#806030"),
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 8, 0, 2)
            });
            column.Children.Add(BuildContributorRewardLines(bounty, isFirst ? 230 : 185));
        }

        var pedestal = new Border
        {
            Width = isFirst ? 150 : 120,
            Height = pedestalHeight,
            Margin = new Thickness(0, 4, 0, 0),
            CornerRadius = new CornerRadius(6, 6, 2, 2),
            BorderBrush = BrushFromHex(pedestalLight, 0.55),
            BorderThickness = new Thickness(2),
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0.5, 0),
                EndPoint = new Point(0.5, 1),
                GradientStops =
                [
                    new GradientStop((Color)ColorConverter.ConvertFromString(pedestalDark)!, 0),
                    new GradientStop((Color)ColorConverter.ConvertFromString(pedestalDarker)!, 0.5),
                    new GradientStop((Color)ColorConverter.ConvertFromString(pedestalDarker)!, 1)
                ]
            },
            Effect = new DropShadowEffect { BlurRadius = 14, ShadowDepth = 4, Opacity = 0.45, Color = Colors.Black },
            Child = new Grid()
        };

        var face = (Grid)pedestal.Child;
        face.Children.Add(new Border
        {
            Height = 5,
            VerticalAlignment = VerticalAlignment.Top,
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops =
                [
                    new GradientStop(BrushFromHex(pedestalLight).Color, 0),
                    new GradientStop(Color.FromArgb(0, 255, 255, 255), 1)
                ]
            }
        });
        face.Children.Add(new TextBlock
        {
            Text = rank.ToString(),
            FontSize = isFirst ? 48 : 34,
            FontWeight = FontWeights.Bold,
            FontFamily = new FontFamily("Georgia"),
            Foreground = BrushFromHex(pedestalLight),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Effect = new DropShadowEffect { BlurRadius = 8, ShadowDepth = 2, Opacity = 0.6, Color = Colors.Black }
        });

        column.Children.Add(pedestal);
        return column;
    }

    private static Border BuildRankedPosterRow(int rank, BountyEntry b)
    {
        var tierFg = BrushFromHex(b.TierForegroundHex);
        var parchment = BrushFromHex("#D4C4A0");
        var classColor = GetClassBrush(b.TargetClass);
        var nameText = string.IsNullOrWhiteSpace(b.AltName) ? b.TargetName : $"{b.TargetName} / {b.AltName}";
        var rankLabel = rank switch { 1 => "🥇", 2 => "🥈", 3 => "🥉", _ => $"#{rank}" };

        var rowGrid = new Grid { Margin = new Thickness(0, 4, 0, 4) };
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var accent = new Border
        {
            Background = BrushFromHex(b.TierBorderHex),
            Margin = new Thickness(-4, -3, -4, -3),
            CornerRadius = new CornerRadius(3)
        };
        Grid.SetColumnSpan(accent, 5);
        rowGrid.Children.Add(accent);

        AddRowCell(rowGrid, rankLabel, 0, parchment, 12, FontWeights.Bold);
        AddRowCell(rowGrid, nameText, 1, classColor, 13, FontWeights.Bold);
        AddRowCellElement(rowGrid, BountyPosterRewardDisplay.Build(b, fontSize: 12, iconSize: 14, center: false), 2);
        AddRowCell(rowGrid, b.Reason, 3, parchment, 11, FontWeights.SemiBold, FontStyles.Italic, wrap: true);
        AddRowCell(rowGrid, b.ContributorDetails, 4, BrushFromHex("#FFD700"), 11, FontWeights.Normal, wrap: true);

        if (rank <= 3)
        {
            accent.Opacity = 0.35;
            rowGrid.Effect = new DropShadowEffect
            {
                BlurRadius = 8, ShadowDepth = 0, Opacity = 0.25,
                Color = ((SolidColorBrush)tierFg).Color
            };
        }
        else
        {
            accent.Opacity = 0.15;
        }

        return new Border
        {
            Child = rowGrid,
            BorderThickness = new Thickness(4, 0, 0, 0),
            BorderBrush = tierFg,
            Padding = new Thickness(4, 0, 0, 0)
        };
    }

    private static SolidColorBrush BrushFromHex(string hex, double opacity = 1)
    {
        var color = (Color)ColorConverter.ConvertFromString(hex)!;
        if (opacity < 1)
            color = Color.FromArgb((byte)(opacity * 255), color.R, color.G, color.B);
        return new SolidColorBrush(color);
    }

    private static void AddHeaderCell(Grid grid, string text, int col, Brush fg)
    {
        var tb = new TextBlock
        {
            Text = text, FontSize = 10, FontWeight = FontWeights.Bold,
            FontFamily = new FontFamily("Georgia"), Foreground = fg,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(6, 0, 6, 0)
        };
        Grid.SetColumn(tb, col);
        grid.Children.Add(tb);
    }

    private static void AddRowCell(Grid grid, string text, int col, Brush fg,
        double fontSize, FontWeight weight, FontStyle? style = null, bool wrap = false)
    {
        var tb = new TextBlock
        {
            Text = text ?? "", FontSize = fontSize, FontWeight = weight,
            FontFamily = new FontFamily("Georgia"), Foreground = fg,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
            TextTrimming = wrap ? TextTrimming.None : TextTrimming.CharacterEllipsis,
            Margin = new Thickness(6, 0, 6, 0)
        };
        if (style.HasValue) tb.FontStyle = style.Value;
        Grid.SetColumn(tb, col);
        grid.Children.Add(tb);
    }

    private static void AddRowCellElement(Grid grid, UIElement element, int col)
    {
        if (element is FrameworkElement fe)
        {
            fe.VerticalAlignment = VerticalAlignment.Center;
            fe.Margin = new Thickness(6, 0, 6, 0);
        }

        Grid.SetColumn(element, col);
        grid.Children.Add(element);
    }

    private static UIElement BuildContributorRewardLines(BountyEntry bounty, double maxWidth)
    {
        var stack = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            MaxWidth = maxWidth,
            Margin = new Thickness(0, 0, 0, 6)
        };

        foreach (var contributor in bounty.Contributors)
        {
            var line = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 2)
            };
            line.Children.Add(new TextBlock
            {
                Text = $"{contributor.Name} (",
                FontSize = 9,
                Foreground = BrushFromHex("#D4C4A0"),
                VerticalAlignment = VerticalAlignment.Center
            });
            line.Children.Add(BountyPosterRewardDisplay.Build(contributor.GoldAmount, fontSize: 9, iconSize: 11, center: false));
            line.Children.Add(new TextBlock
            {
                Text = ")",
                FontSize = 9,
                Foreground = BrushFromHex("#D4C4A0"),
                VerticalAlignment = VerticalAlignment.Center
            });
            stack.Children.Add(line);
        }

        return stack;
    }

    private static TextBlock MakeOrnament(SolidColorBrush brush)
    {
        return new TextBlock
        {
            Text = "══════════════════════════════",
            FontSize = 14, Foreground = brush,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 4)
        };
    }

    private static Border MakeSeparator(string colorHex, int verticalMargin = 8)
    {
        return new Border
        {
            Height = 2, Margin = new Thickness(40, verticalMargin, 40, verticalMargin),
            Background = new LinearGradientBrush
            {
                GradientStops =
                [
                    new GradientStop((Color)ColorConverter.ConvertFromString($"#00{colorHex[1..]}"), 0),
                    new GradientStop((Color)ColorConverter.ConvertFromString(colorHex), 0.5),
                    new GradientStop((Color)ColorConverter.ConvertFromString($"#00{colorHex[1..]}"), 1)
                ]
            }
        };
    }

    private static void RenderAndCopyPoster(BountyEntry bounty)
    {
        try
        {
            BountyPosterRewardDisplay.WarmupIcons();
            var poster = BuildWantedPoster(bounty);
            var size = new Size(500, 650);
            poster.Measure(size);
            poster.Arrange(new Rect(size));
            poster.UpdateLayout();

            var dpi = 192;
            var pixelW = (int)(size.Width * 2);
            var pixelH = (int)(size.Height * 2);

            var rtb = new RenderTargetBitmap(pixelW, pixelH, dpi, dpi, PixelFormats.Pbgra32);
            rtb.Render(poster);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));

            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Primes");
            Directory.CreateDirectory(dir);
            var safeName = string.Join("_", bounty.TargetName.Split(Path.GetInvalidFileNameChars()));
            var filePath = Path.Combine(dir, $"Avis_{safeName}_{DateTime.Now:yyyyMMdd_HHmmss}.png");

            using (var fs = new FileStream(filePath, FileMode.Create))
                encoder.Save(fs);

            try { Clipboard.SetImage(rtb); } catch { }

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(filePath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erreur generation avis :\n{ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static Border BuildWantedPoster(BountyEntry bounty)
    {
        var classColor = GetClassBrush(bounty.TargetClass);
        var gold = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFD700"));
        var parchment = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D4C4A0"));
        var darkGold = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D4A017"));
        var brown = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#806030"));
        var darkBrown = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5A4020"));

        var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };

        // --- Top ornament ---
        stack.Children.Add(new TextBlock
        {
            Text = "══════════════════════════",
            FontSize = 14,
            Foreground = brown,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 8)
        });

        // --- Title ---
        stack.Children.Add(new TextBlock
        {
            Text = "AVIS DE RECHERCHE",
            FontSize = 28,
            FontWeight = FontWeights.Bold,
            FontFamily = new FontFamily("Georgia"),
            Foreground = darkGold,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 4)
        });

        stack.Children.Add(new TextBlock
        {
            Text = "MORT OU VIF",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            FontFamily = new FontFamily("Georgia"),
            Foreground = brown,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 16)
        });

        // --- Separator ---
        stack.Children.Add(new Border
        {
            Height = 2,
            Margin = new Thickness(40, 0, 40, 16),
            Background = new LinearGradientBrush(
                (Color)ColorConverter.ConvertFromString("#00806030"),
                (Color)ColorConverter.ConvertFromString("#806030"),
                0)
            {
                GradientStops =
                [
                    new GradientStop((Color)ColorConverter.ConvertFromString("#00806030"), 0),
                    new GradientStop((Color)ColorConverter.ConvertFromString("#806030"), 0.5),
                    new GradientStop((Color)ColorConverter.ConvertFromString("#00806030"), 1)
                ]
            }
        });

        // --- Target name ---
        stack.Children.Add(new TextBlock
        {
            Text = bounty.TargetName.ToUpperInvariant(),
            FontSize = 36,
            FontWeight = FontWeights.Bold,
            FontFamily = new FontFamily("Georgia"),
            Foreground = classColor,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 4),
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        });

        if (!string.IsNullOrWhiteSpace(bounty.AltName))
        {
            stack.Children.Add(new TextBlock
            {
                Text = $"alias \"{bounty.AltName}\"",
                FontSize = 14,
                FontStyle = FontStyles.Italic,
                FontFamily = new FontFamily("Georgia"),
                Foreground = parchment,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 6)
            });
        }

        // --- Class + Race ---
        var classRace = new List<string>();
        if (!string.IsNullOrWhiteSpace(bounty.TargetRace)) classRace.Add(bounty.TargetRace);
        if (!string.IsNullOrWhiteSpace(bounty.TargetClass)) classRace.Add(bounty.TargetClass);
        if (classRace.Count > 0)
        {
            stack.Children.Add(new TextBlock
            {
                Text = string.Join(" — ", classRace),
                FontSize = 16,
                FontFamily = new FontFamily("Georgia"),
                Foreground = parchment,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 16)
            });
        }

        // --- Reason ---
        if (!string.IsNullOrWhiteSpace(bounty.Reason))
        {
            stack.Children.Add(new TextBlock
            {
                Text = $"\"{bounty.Reason}\"",
                FontSize = 14,
                FontStyle = FontStyles.Italic,
                FontFamily = new FontFamily("Georgia"),
                Foreground = darkBrown,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(20, 0, 20, 16),
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center
            });
        }

        // --- Separator ---
        stack.Children.Add(new Border
        {
            Height = 2,
            Margin = new Thickness(40, 0, 40, 16),
            Background = new LinearGradientBrush
            {
                GradientStops =
                [
                    new GradientStop((Color)ColorConverter.ConvertFromString("#00D4A017"), 0),
                    new GradientStop((Color)ColorConverter.ConvertFromString("#D4A017"), 0.5),
                    new GradientStop((Color)ColorConverter.ConvertFromString("#00D4A017"), 1)
                ]
            }
        });

        // --- Reward ---
        stack.Children.Add(new TextBlock
        {
            Text = "RECOMPENSE",
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            FontFamily = new FontFamily("Georgia"),
            Foreground = darkGold,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 4)
        });

        var singleReward = BountyPosterRewardDisplay.Build(bounty, fontSize: 32, iconSize: 28);
        if (singleReward is FrameworkElement rewardFe)
            rewardFe.Margin = new Thickness(0, 0, 0, 16);
        stack.Children.Add(singleReward);

        // --- Contributors ---
        if (bounty.Contributors.Count > 0)
        {
            stack.Children.Add(new TextBlock
            {
                Text = "Commanditaires :",
                FontSize = 12,
                FontFamily = new FontFamily("Georgia"),
                Foreground = darkBrown,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 4)
            });

            var contribText = string.Join(", ", bounty.Contributors.Select(c => $"{c.Name} ({c.GoldAmount}po)"));
            stack.Children.Add(new TextBlock
            {
                Text = contribText,
                FontSize = 12,
                FontFamily = new FontFamily("Georgia"),
                Foreground = parchment,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(20, 0, 20, 8)
            });
        }

        // --- Bottom ornament ---
        stack.Children.Add(new TextBlock
        {
            Text = "══════════════════════════",
            FontSize = 14,
            Foreground = brown,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 0)
        });

        var poster = new Border
        {
            Width = 500,
            MinHeight = 550,
            Padding = new Thickness(30),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(3),
            BorderBrush = BrushFromHex(bounty.TierBorderHex),
            Effect = new DropShadowEffect
            {
                BlurRadius = 20, ShadowDepth = 3, Opacity = 0.5,
                Color = ((SolidColorBrush)BrushFromHex(bounty.TierForegroundHex)).Color
            },
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0.5, 0),
                EndPoint = new Point(0.5, 1),
                GradientStops =
                [
                    new GradientStop((Color)ColorConverter.ConvertFromString("#3A2A14"), 0),
                    new GradientStop((Color)ColorConverter.ConvertFromString("#2A1E0E"), 0.3),
                    new GradientStop((Color)ColorConverter.ConvertFromString("#1A120A"), 0.7),
                    new GradientStop((Color)ColorConverter.ConvertFromString("#2A1E0E"), 1)
                ]
            },
            Child = stack
        };

        return poster;
    }

    private static SolidColorBrush GetClassBrush(string className)
    {
        if (!string.IsNullOrEmpty(className) && ClassColors.TryGetValue(className, out var hex))
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D4C4A0"));
    }

}
