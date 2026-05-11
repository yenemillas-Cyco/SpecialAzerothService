using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using WindowsOrganiserApp.Models.Carto;
using WindowsOrganiserApp.ViewModels;

namespace WindowsOrganiserApp.Views;

public partial class CartoView : UserControl
{
    private CartoViewModel Vm => (CartoViewModel)DataContext;
    private bool _isPanning;
    private Point _panStart;
    private double _panStartOffsetX, _panStartOffsetY;

    public CartoView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is CartoViewModel vm)
            {
                vm.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName is nameof(CartoViewModel.FilteredCharacters)
                        or nameof(CartoViewModel.SelectedCharacter))
                        RedrawAll();
                    else if (e.PropertyName == nameof(CartoViewModel.OverlayChanged))
                        RedrawAll();
                };
                RedrawAll();
            }
        };
        Loaded += (_, _) =>
        {
            MapImage.SizeChanged += (_, _) => RedrawAll();
            RedrawAll();
        };
    }

    private double MapWidth => MapImage.ActualWidth > 0 ? MapImage.ActualWidth : 1024;
    private double MapHeight => MapImage.ActualHeight > 0 ? MapImage.ActualHeight : 768;

    private void RedrawAll()
    {
        RedrawOverlays();
        RedrawMarkers();
    }

    private void RedrawOverlays()
    {
        if (Vm == null) return;

        // Remove old overlays
        for (int i = MapCanvas.Children.Count - 1; i >= 0; i--)
        {
            if (MapCanvas.Children[i] is FrameworkElement { Tag: "overlay" or "fp_line" or "fp_node" })
                MapCanvas.Children.RemoveAt(i);
        }

        var w = MapWidth;
        var h = MapHeight;

        // Zone names
        if (Vm.ShowZoneNames)
        {
            foreach (var zone in MapOverlayData.Zones)
            {
                var name = Vm.UseFrencNames ? zone.NameFR : zone.NameEN;
                var levelText = Vm.ShowZoneLevels ? $" ({zone.LevelMin}-{zone.LevelMax})" : "";

                var tb = new TextBlock
                {
                    Text = name + levelText,
                    FontSize = 9,
                    Foreground = new SolidColorBrush(Color.FromArgb(220, 255, 210, 100)),
                    FontWeight = FontWeights.SemiBold,
                    Tag = "overlay"
                };
                tb.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black, BlurRadius = 3, ShadowDepth = 1, Opacity = 0.9
                };
                Canvas.SetLeft(tb, zone.X * w);
                Canvas.SetTop(tb, zone.Y * h);
                MapCanvas.Children.Add(tb);
            }
        }

        // Alliance flight paths
        if (Vm.ShowAllianceFlightPaths)
        {
            DrawFlightPaths(MapOverlayData.AllianceRoutes, MapOverlayData.FlightNodes,
                Color.FromRgb(68, 136, 255), Faction.Alliance, w, h);
        }

        // Horde flight paths
        if (Vm.ShowHordeFlightPaths)
        {
            DrawFlightPaths(MapOverlayData.HordeRoutes, MapOverlayData.FlightNodes,
                Color.FromRgb(255, 68, 68), Faction.Horde, w, h);
        }
    }

    private void DrawFlightPaths(FlightRoute[] routes, FlightNode[] nodes, Color color,
        Faction faction, double w, double h)
    {
        var brush = new SolidColorBrush(color);
        var lineBrush = new SolidColorBrush(Color.FromArgb(140, color.R, color.G, color.B));

        // Draw routes as lines
        foreach (var route in routes)
        {
            if (route.FromIndex >= nodes.Length || route.ToIndex >= nodes.Length) continue;
            var from = nodes[route.FromIndex];
            var to = nodes[route.ToIndex];

            var line = new Line
            {
                X1 = from.X * w, Y1 = from.Y * h,
                X2 = to.X * w, Y2 = to.Y * h,
                Stroke = lineBrush,
                StrokeThickness = 1.5,
                StrokeDashArray = [4, 2],
                Tag = "fp_line"
            };
            MapCanvas.Children.Add(line);
        }

        // Draw nodes
        foreach (var node in nodes)
        {
            if (node.Faction != faction && node.Faction != Faction.Neutral) continue;

            var nodeBrush = node.Faction == Faction.Neutral
                ? new SolidColorBrush(Color.FromRgb(255, 215, 0))
                : brush;

            var dot = new Ellipse
            {
                Width = 8, Height = 8,
                Fill = nodeBrush,
                Stroke = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                StrokeThickness = 0.5,
                Tag = "fp_node",
                ToolTip = Vm.UseFrencNames ? node.NameFR : node.NameEN
            };
            Canvas.SetLeft(dot, node.X * w - 4);
            Canvas.SetTop(dot, node.Y * h - 4);
            MapCanvas.Children.Add(dot);
        }
    }

    private void RedrawMarkers()
    {
        if (Vm == null) return;

        // Remove old character markers only
        for (int i = MapCanvas.Children.Count - 1; i >= 0; i--)
        {
            if (MapCanvas.Children[i] is Ellipse { Tag: WowCharacter }
                || MapCanvas.Children[i] is Border { Tag: "marker" })
                MapCanvas.Children.RemoveAt(i);
        }

        foreach (var ch in Vm.FilteredCharacters)
        {
            var color = WowClassColors.GetHexColor(ch.Class);
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
            var isSelected = ch == Vm.SelectedCharacter;

            var marker = new Ellipse
            {
                Width = isSelected ? 16 : 12,
                Height = isSelected ? 16 : 12,
                Fill = brush,
                Stroke = isSelected ? Brushes.White : new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                StrokeThickness = isSelected ? 2 : 1,
                Cursor = Cursors.Hand,
                Tag = ch
            };
            marker.ToolTip = $"{ch.Name} ({ch.Class}) Lv.{ch.Level}";

            Canvas.SetLeft(marker, ch.MapX - marker.Width / 2);
            Canvas.SetTop(marker, ch.MapY - marker.Height / 2);
            MapCanvas.Children.Add(marker);

            // Name label
            var label = new Border
            {
                Tag = "marker",
                Child = new TextBlock
                {
                    Text = ch.Name,
                    FontSize = 9,
                    Foreground = brush,
                    FontWeight = isSelected ? FontWeights.Bold : FontWeights.Normal
                },
                Background = new SolidColorBrush(Color.FromArgb(180, 20, 15, 5)),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(2, 0, 2, 0)
            };
            Canvas.SetLeft(label, ch.MapX + 8);
            Canvas.SetTop(label, ch.MapY - 8);
            MapCanvas.Children.Add(label);
        }

        UpdateSelectedCharacterPanel();
    }

    private void UpdateSelectedCharacterPanel()
    {
        if (Vm.SelectedCharacter is { } ch)
        {
            var accountName = Vm.Accounts.FirstOrDefault(a => a.Id == ch.AccountId)?.Name ?? "—";
            SelectedCharInfo.Text = $"{ch.Name} — {ch.Class} Lv.{ch.Level}\nCompte: {accountName}";

            // Rebuild cooldowns list
            CooldownsList.Items.Clear();
            foreach (var cd in ch.Cooldowns)
            {
                var panel = new DockPanel { Margin = new Thickness(0, 0, 0, 2) };

                // Delete button
                var btnDel = new Button
                {
                    Content = "✕", FontSize = 9,
                    Padding = new Thickness(3, 0, 3, 0), Tag = cd,
                    ToolTip = "Supprimer ce cooldown"
                };
                btnDel.Click += RemoveCooldown_Click;
                DockPanel.SetDock(btnDel, Dock.Right);
                panel.Children.Add(btnDel);

                // Edit timer button
                var btnEdit = new Button
                {
                    Content = "✎", FontSize = 9,
                    Padding = new Thickness(3, 0, 3, 0), Tag = cd, Margin = new Thickness(2, 0, 0, 0),
                    ToolTip = "Modifier le timer"
                };
                btnEdit.Click += EditCooldownTimer_Click;
                DockPanel.SetDock(btnEdit, Dock.Right);
                panel.Children.Add(btnEdit);

                // Activate button
                var btn = new Button
                {
                    Content = "↻", FontSize = 10,
                    Padding = new Thickness(4, 1, 4, 1), Tag = cd, Margin = new Thickness(2, 0, 0, 0),
                    ToolTip = "Lancer le cooldown"
                };
                btn.Click += ActivateCooldown_Click;
                DockPanel.SetDock(btn, Dock.Right);
                panel.Children.Add(btn);

                var status = cd.IsReady ? "✅ PRÊT" : $"⏳ {FormatTimeSpan(cd.TimeRemaining)}";
                panel.Children.Add(new TextBlock
                {
                    Text = $"{cd.Type}: {status}",
                    FontSize = 10,
                    Foreground = cd.IsReady
                        ? new SolidColorBrush(Color.FromRgb(100, 255, 100))
                        : new SolidColorBrush(Color.FromRgb(255, 200, 100)),
                    VerticalAlignment = VerticalAlignment.Center
                });
                CooldownsList.Items.Add(panel);
            }

            // Rebuild quest items list
            QuestItemsList.Items.Clear();
            foreach (var qi in ch.QuestItems)
            {
                var panel = new DockPanel { Margin = new Thickness(0, 0, 0, 2) };
                var btnDone = new Button
                {
                    Content = "✓",
                    FontSize = 10,
                    Padding = new Thickness(4, 1, 4, 1),
                    Tag = qi
                };
                btnDone.Click += MarkQuestItemDone_Click;
                DockPanel.SetDock(btnDone, Dock.Right);
                panel.Children.Add(btnDone);

                var planned = qi.PlannedTurnIn.HasValue
                    ? $" → {qi.PlannedTurnIn.Value:dd/MM HH:mm}"
                    : "";
                panel.Children.Add(new TextBlock
                {
                    Text = $"{FormatQuestItem(qi.Type)}{planned}",
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(255, 215, 0)),
                    VerticalAlignment = VerticalAlignment.Center
                });
                QuestItemsList.Items.Add(panel);
            }

            // Rebuild professions list
            ProfessionsList.Items.Clear();
            foreach (var p in ch.Professions)
            {
                ProfessionsList.Items.Add(new TextBlock
                {
                    Text = $"{p.Type} ({p.Skill}/300)",
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                    Margin = new Thickness(0, 0, 0, 1)
                });
            }
        }
        else
        {
            SelectedCharInfo.Text = "";
            CooldownsList.Items.Clear();
            QuestItemsList.Items.Clear();
            ProfessionsList.Items.Clear();
        }
    }

    private void MapCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (Vm.IsPlacingCharacter)
        {
            var pos = e.GetPosition(MapImage);
            Vm.PlaceCharacterAt(pos.X, pos.Y);
            RedrawAll();
            e.Handled = true;
            return;
        }

        // Check if clicking on a marker
        if (e.OriginalSource is Ellipse { Tag: WowCharacter ch })
        {
            Vm.SelectedCharacter = ch;
            RedrawAll();
            e.Handled = true;
            return;
        }

        // Start panning
        _isPanning = true;
        _panStart = e.GetPosition(MapBorder);
        _panStartOffsetX = Vm.MapOffsetX;
        _panStartOffsetY = Vm.MapOffsetY;
        MapContainer.CaptureMouse();
        e.Handled = true;
    }

    private void MapCanvas_RightMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (Vm.IsPlacingCharacter)
        {
            Vm.IsPlacingCharacter = false;
            e.Handled = true;
        }
    }

    private void MapCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isPanning) return;
        var pos = e.GetPosition(MapBorder);
        Vm.MapOffsetX = _panStartOffsetX + (pos.X - _panStart.X);
        Vm.MapOffsetY = _panStartOffsetY + (pos.Y - _panStart.Y);
    }

    private void MapCanvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _isPanning = false;
        MapContainer.ReleaseMouseCapture();
    }

    private void MapCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Delta > 0)
            Vm.ZoomInCommand.Execute(null);
        else
            Vm.ZoomOutCommand.Execute(null);
        e.Handled = true;
    }

    private void AddCooldown_Click(object sender, RoutedEventArgs e)
    {
        if (CdTypeCombo.SelectedItem is CooldownType type)
            Vm.AddCooldownCommand.Execute(type);
        RedrawMarkers();
    }

    private void AddQuestItem_Click(object sender, RoutedEventArgs e)
    {
        if (QiTypeCombo.SelectedItem is QuestItemType type)
            Vm.AddQuestItemCommand.Execute(type);
        RedrawMarkers();
    }

    private void AddProfession_Click(object sender, RoutedEventArgs e)
    {
        if (ProfTypeCombo.SelectedItem is ProfessionType type)
            Vm.AddProfessionCommand.Execute(type);
        RedrawMarkers();
    }

    private void ActivateCooldown_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: CooldownEntry cd })
        {
            Vm.ActivateCooldownCommand.Execute(cd);
            RedrawMarkers();
        }
    }

    private void MarkQuestItemDone_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: QuestItemEntry qi })
        {
            Vm.MarkQuestItemTurnedInCommand.Execute(qi);
            RedrawMarkers();
        }
    }

    private void RemoveCharacter_Click(object sender, RoutedEventArgs e)
    {
        if (Vm.SelectedCharacter != null)
        {
            Vm.RemoveCharacterCommand.Execute(Vm.SelectedCharacter);
            RedrawMarkers();
        }
    }

    private void MoveCharacter_Click(object sender, RoutedEventArgs e)
    {
        if (Vm.SelectedCharacter != null)
            Vm.MoveCharacterCommand.Execute(Vm.SelectedCharacter);
    }

    private void AddAccount_Click(object sender, RoutedEventArgs e) => DoAddAccount();

    private void NewAccountBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Return)
        {
            DoAddAccount();
            e.Handled = true;
        }
    }

    private void DoAddAccount()
    {
        var name = NewAccountBox.Text.Trim();
        if (!string.IsNullOrEmpty(name))
        {
            Vm.AddAccountCommand.Execute(name);
            NewAccountBox.Text = string.Empty;
        }
    }

    private void RemoveAccount_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: WowAccount account })
            Vm.RemoveAccountCommand.Execute(account);
    }

    private void RemoveCooldown_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: CooldownEntry cd })
        {
            Vm.RemoveCooldownCommand.Execute(cd);
            RedrawAll();
        }
    }

    private void EditCooldownTimer_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: CooldownEntry cd })
        {
            var defaultVal = cd.TimeRemaining?.TotalHours.ToString("F1") ?? "0";
            var input = PromptInput("Modifier le timer", "Heures restantes (ex: 12.5) :", defaultVal);
            if (input != null && double.TryParse(input, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var hours))
            {
                cd.LastUsed = DateTime.Now - (cd.Duration - TimeSpan.FromHours(hours));
                cd.Note = null;
                Vm.Save();
                RedrawAll();
            }
        }
    }

    private static string? PromptInput(string title, string message, string defaultValue)
    {
        var win = new Window
        {
            Title = title, Width = 320, Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize, WindowStyle = WindowStyle.ToolWindow
        };
        var stack = new StackPanel { Margin = new Thickness(12) };
        stack.Children.Add(new TextBlock { Text = message, Margin = new Thickness(0, 0, 0, 8) });
        var tb = new TextBox { Text = defaultValue };
        stack.Children.Add(tb);
        var btn = new Button { Content = "OK", Margin = new Thickness(0, 8, 0, 0), Padding = new Thickness(20, 4, 20, 4), HorizontalAlignment = HorizontalAlignment.Center };
        string? result = null;
        btn.Click += (_, _) => { result = tb.Text; win.Close(); };
        stack.Children.Add(btn);
        win.Content = stack;
        tb.Focus();
        tb.SelectAll();
        win.ShowDialog();
        return result;
    }

    private static string FormatTimeSpan(TimeSpan? ts)
    {
        if (ts == null) return "—";
        return ts.Value.TotalHours >= 1
            ? $"{(int)ts.Value.TotalHours}h{ts.Value.Minutes:D2}"
            : $"{ts.Value.Minutes}m";
    }

    private static string FormatQuestItem(QuestItemType type) => type switch
    {
        QuestItemType.Tete_de_Rend => "Tête de Rend",
        QuestItemType.Tete_dOnyxia => "Tête d'Onyxia",
        QuestItemType.Tete_de_Nefarian => "Tête de Nefarian",
        QuestItemType.Coeur_de_Hakkar => "Cœur de Hakkar",
        _ => type.ToString()
    };
}
