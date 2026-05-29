using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SpecialAzerothService.Core.Models.Bounty;
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
        RenderAndCopyFullPoster(Vm.Rules, Vm.Bounties.Where(b => !b.IsCompleted).ToList());
    }

    private static void RenderAndCopyFullPoster(string rules, List<BountyEntry> bounties)
    {
        try
        {
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
        var gold = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFD700"));
        var darkGold = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D4A017"));
        var parchment = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D4C4A0"));
        var brown = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#806030"));
        var darkBrown = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5A4020"));
        var dimText = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A09070"));

        var stack = new StackPanel();

        // ornament
        stack.Children.Add(MakeOrnament(brown));

        // title
        stack.Children.Add(new TextBlock
        {
            Text = "AVIS DE RECHERCHE — MORTS OU VIFS",
            FontSize = 26, FontWeight = FontWeights.Bold, FontFamily = new FontFamily("Georgia"),
            Foreground = darkGold, HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 10)
        });

        // separator
        stack.Children.Add(MakeSeparator("#806030"));

        // rules
        if (!string.IsNullOrWhiteSpace(rules))
        {
            stack.Children.Add(new TextBlock
            {
                Text = "REGLEMENT",
                FontSize = 12, FontWeight = FontWeights.Bold, FontFamily = new FontFamily("Georgia"),
                Foreground = darkGold, HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 8, 0, 4)
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

        // table header
        var headerGrid = new Grid { Margin = new Thickness(0, 4, 0, 4) };
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(320) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        AddHeaderCell(headerGrid, "CIBLE", 0, darkGold);
        AddHeaderCell(headerGrid, "PRIME", 1, darkGold);
        AddHeaderCell(headerGrid, "MOTIF", 2, darkGold);
        AddHeaderCell(headerGrid, "COMMANDITAIRES", 3, darkGold);
        stack.Children.Add(headerGrid);

        stack.Children.Add(new Border
        {
            Height = 1, Margin = new Thickness(10, 0, 10, 4),
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#50806030"))
        });

        // bounty rows
        for (int i = 0; i < bounties.Count; i++)
        {
            var b = bounties[i];
            var classColor = GetClassBrush(b.TargetClass);

            var rowGrid = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(320) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var nameText = string.IsNullOrWhiteSpace(b.AltName) ? b.TargetName : $"{b.TargetName} / {b.AltName}";
            AddRowCell(rowGrid, nameText, 0, classColor, 13, FontWeights.Bold);
            AddRowCell(rowGrid, b.DisplayTotal, 1, gold, 13, FontWeights.Bold);
            AddRowCell(rowGrid, b.Reason, 2, parchment, 12, FontWeights.SemiBold, FontStyles.Italic, wrap: true);
            AddRowCell(rowGrid, b.ContributorDetails, 3, gold, 11, FontWeights.Normal, wrap: true);

            if (i % 2 == 1)
            {
                rowGrid.Children.Insert(0, new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#08D4A017")),
                    Margin = new Thickness(-5, -2, -5, -2)
                });
                Grid.SetColumnSpan(rowGrid.Children[0] as UIElement, 4);
            }

            stack.Children.Add(rowGrid);
        }

        // total
        stack.Children.Add(MakeSeparator("#D4A017", 10));
        var total = bounties.Sum(b => b.TotalGold);
        stack.Children.Add(new TextBlock
        {
            Text = $"TOTAL DES PRIMES : {total} PIECES D'OR",
            FontSize = 18, FontWeight = FontWeights.Bold, FontFamily = new FontFamily("Georgia"),
            Foreground = gold, HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 8)
        });

        stack.Children.Add(MakeOrnament(brown));

        return new Border
        {
            Width = 900,
            Padding = new Thickness(30, 20, 30, 20),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(3),
            BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8B7355")),
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

        stack.Children.Add(new TextBlock
        {
            Text = $"{bounty.TotalGold} PIECES D'OR",
            FontSize = 30,
            FontWeight = FontWeights.Bold,
            FontFamily = new FontFamily("Georgia"),
            Foreground = gold,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 16)
        });

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
            BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8B7355")),
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
