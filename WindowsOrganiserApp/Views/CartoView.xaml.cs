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
    private WowCharacter? _tooltipCharacter;

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
                var levelText = (Vm.ShowZoneLevels && zone.LevelMin > 0) ? $" ({zone.LevelMin}-{zone.LevelMax})" : "";

                Color textColor;
                double fontSize;
                if (zone.IsCapital)
                {
                    textColor = zone.CapitalFaction == Faction.Alliance
                        ? Color.FromRgb(100, 180, 255)
                        : zone.CapitalFaction == Faction.Horde
                            ? Color.FromRgb(255, 100, 100)
                            : Color.FromRgb(255, 215, 0);
                    fontSize = 11;
                }
                else
                {
                    textColor = Color.FromArgb(220, 255, 210, 100);
                    fontSize = 9;
                }

                var tb = new TextBlock
                {
                    Text = name + levelText,
                    FontSize = fontSize,
                    Foreground = new SolidColorBrush(textColor),
                    FontWeight = zone.IsCapital ? FontWeights.Bold : FontWeights.SemiBold,
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
            var size = isSelected ? 16.0 : 12.0;

            var marker = new Ellipse
            {
                Width = size, Height = size,
                Fill = brush,
                Stroke = isSelected ? Brushes.White : new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                StrokeThickness = isSelected ? 2 : 1,
                Cursor = Cursors.Hand,
                Tag = ch
            };

            Canvas.SetLeft(marker, ch.MapX - size / 2);
            Canvas.SetTop(marker, ch.MapY - size / 2);
            MapCanvas.Children.Add(marker);

            // Name label above the point
            var accountName = Vm.Accounts.FirstOrDefault(a => a.Id == ch.AccountId)?.Name;
            var labelText = accountName != null ? $"{ch.Name} ({accountName})" : ch.Name;
            var label = new Border
            {
                Tag = "marker",
                Child = new TextBlock
                {
                    Text = labelText,
                    FontSize = 9,
                    Foreground = brush,
                    FontWeight = isSelected ? FontWeights.Bold : FontWeights.Normal,
                    TextAlignment = TextAlignment.Center
                },
                Background = new SolidColorBrush(Color.FromArgb(200, 15, 12, 5)),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(3, 1, 3, 1)
            };
            Canvas.SetLeft(label, ch.MapX - 20);
            Canvas.SetTop(label, ch.MapY - size / 2 - 16);
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

        // Toggle tooltip on marker click
        if (e.OriginalSource is Ellipse { Tag: WowCharacter ch })
        {
            if (_tooltipCharacter == ch && CharPopup.IsOpen)
            {
                CloseCharacterTooltip();
            }
            else
            {
                Vm.SelectedCharacter = ch;
                RedrawAll();
                ShowCharacterTooltip(ch);
            }
            e.Handled = true;
            return;
        }

        // Close tooltip if open and clicking outside marker
        if (CharPopup.IsOpen)
            CloseCharacterTooltip();

        // Start panning
        _isPanning = true;
        _panStart = e.GetPosition(MapBorder);
        _panStartOffsetX = Vm.MapOffsetX;
        _panStartOffsetY = Vm.MapOffsetY;
        MapBorder.CaptureMouse();
        e.Handled = true;
    }

    private void MapCanvas_RightMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (Vm.IsPlacingCharacter)
        {
            Vm.CancelPlacement();
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
        MapBorder.ReleaseMouseCapture();
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

    private void SummaryGrid_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SummaryGrid.SelectedItem is WowCharacter ch)
            ShowCharacterTooltip(ch);
    }

    private void CloseCharacterTooltip()
    {
        CharPopup.IsOpen = false;
        _tooltipCharacter = null;
        Vm.SelectedCharacter = null;
        RedrawAll();
    }

    private void ShowCharacterTooltip(WowCharacter ch)
    {
        _tooltipCharacter = ch;
        RebuildTooltipContent(ch);
        CharPopup.IsOpen = true;
    }

    private void RebuildTooltipContent(WowCharacter ch)
    {
        CharPopupContent.Children.Clear();
        var stack = CharPopupContent;
        var classColor = (Color)ColorConverter.ConvertFromString(WowClassColors.GetHexColor(ch.Class));
        var classBrush = new SolidColorBrush(classColor);
        var goldBrush = Brushes.Gold;
        var dimBrush = new SolidColorBrush(Color.FromRgb(180, 180, 180));
        var bgInput = new SolidColorBrush(Color.FromRgb(40, 35, 20));

        // ── Header ──
        stack.Children.Add(new TextBlock
        {
            Text = $"{ch.Name}  —  {ch.Class}  Lv.{ch.Level}",
            FontSize = 12, FontWeight = FontWeights.Bold, Foreground = classBrush
        });
        var accountName = Vm.Accounts.FirstOrDefault(a => a.Id == ch.AccountId)?.Name ?? "—";
        stack.Children.Add(new TextBlock
        {
            Text = $"Compte: {accountName}", FontSize = 10, Foreground = Brushes.Gray, Margin = new Thickness(0, 0, 0, 4)
        });

        // ── Actions (toujours visible en haut) ──
        var actionsPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        var btnMove = new Button { Content = "↕ Déplacer", FontSize = 9, Padding = new Thickness(6, 2, 6, 2) };
        btnMove.Click += (_, _) => { CloseCharacterTooltip(); Vm.MoveCharacterCommand.Execute(ch); };
        var btnDelete = new Button { Content = "🗑 Suppr", FontSize = 9, Padding = new Thickness(6, 2, 6, 2), Margin = new Thickness(4, 0, 0, 0), Foreground = Brushes.Red };
        btnDelete.Click += (_, _) =>
        {
            var result = MessageBox.Show(
                $"Supprimer le personnage \"{ch.Name}\" ?",
                "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                CloseCharacterTooltip();
                Vm.RemoveCharacterCommand.Execute(ch);
                RedrawAll();
            }
        };
        var btnClose = new Button { Content = "✓ Fermer", FontSize = 9, Padding = new Thickness(6, 2, 6, 2), Margin = new Thickness(4, 0, 0, 0) };
        btnClose.Click += (_, _) => CloseCharacterTooltip();
        actionsPanel.Children.Add(btnMove);
        actionsPanel.Children.Add(btnDelete);
        actionsPanel.Children.Add(btnClose);
        stack.Children.Add(actionsPanel);

        // ── Shards (Démoniste only) ──
        if (ch.Class == WowClass.Demoniste)
        {
            var shardPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            shardPanel.Children.Add(new TextBlock
            {
                Text = "💎 Shards:", FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 130, 201)),
                VerticalAlignment = VerticalAlignment.Center
            });
            var shardBox = new TextBox
            {
                Text = ch.ShardCount.ToString(), Width = 45, FontSize = 10,
                Padding = new Thickness(3, 1, 3, 1), Margin = new Thickness(6, 0, 0, 0),
                Background = bgInput, Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(148, 130, 201)),
                BorderThickness = new Thickness(1), VerticalAlignment = VerticalAlignment.Center
            };
            shardBox.LostFocus += (_, _) =>
            {
                if (int.TryParse(shardBox.Text, out var val) && val >= 0)
                    ch.ShardCount = val;
                else
                    shardBox.Text = ch.ShardCount.ToString();
                Vm.Save();
            };
            shardPanel.Children.Add(shardBox);
            stack.Children.Add(shardPanel);
        }

        // ── Note (editable) ──
        stack.Children.Add(new TextBlock { Text = "📝 Note", FontSize = 10, Foreground = goldBrush, Margin = new Thickness(0, 2, 0, 2) });
        var noteBox = new TextBox
        {
            Text = ch.Note ?? "", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
            MinHeight = 28, MaxHeight = 60, FontSize = 10,
            Background = bgInput, Foreground = Brushes.White, BorderBrush = Brushes.DarkGoldenrod,
            BorderThickness = new Thickness(1), Padding = new Thickness(3)
        };
        noteBox.LostFocus += (_, _) => { ch.Note = noteBox.Text; Vm.Save(); };
        stack.Children.Add(noteBox);

        // ── Cooldowns ──
        stack.Children.Add(new TextBlock { Text = "⏱ Cooldowns", FontSize = 10, Foreground = goldBrush, Margin = new Thickness(0, 6, 0, 2) });
        foreach (var cd in ch.Cooldowns.ToList())
        {
            var cdRow = new DockPanel { Margin = new Thickness(0, 1, 0, 1) };
            var btnDel = new Button { Content = "✕", FontSize = 8, Padding = new Thickness(3, 0, 3, 0), Margin = new Thickness(2, 0, 0, 0), ToolTip = "Supprimer" };
            btnDel.Click += (_, _) => { ch.Cooldowns.Remove(cd); Vm.Save(); RebuildTooltipContent(ch); };
            var btnActivate = new Button { Content = "↻", FontSize = 9, Padding = new Thickness(3, 0, 3, 0), Margin = new Thickness(2, 0, 0, 0), ToolTip = "Activer maintenant" };
            btnActivate.Click += (_, _) => { cd.LastUsed = DateTime.Now; cd.Note = null; Vm.Save(); RebuildTooltipContent(ch); };
            DockPanel.SetDock(btnDel, Dock.Right);
            DockPanel.SetDock(btnActivate, Dock.Right);
            cdRow.Children.Add(btnDel);
            cdRow.Children.Add(btnActivate);

            var status = cd.IsReady ? "✅ PRÊT" : $"⏳ {FormatTimeSpan(cd.TimeRemaining)}";
            cdRow.Children.Add(new TextBlock
            {
                Text = $"{cd.Type}: {status}", FontSize = 10,
                Foreground = cd.IsReady ? Brushes.LightGreen : Brushes.Orange,
                VerticalAlignment = VerticalAlignment.Center
            });
            stack.Children.Add(cdRow);
        }
        // Add cooldown
        var cdAddPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) };
        var cdCombo = new ComboBox { ItemsSource = Enum.GetValues(typeof(CooldownType)), FontSize = 9, Width = 120, Height = 22 };
        var cdAddBtn = new Button { Content = "+", FontSize = 9, Padding = new Thickness(5, 0, 5, 0), Margin = new Thickness(4, 0, 0, 0) };
        cdAddBtn.Click += (_, _) =>
        {
            if (cdCombo.SelectedItem is CooldownType type && !ch.Cooldowns.Any(c => c.Type == type))
            {
                ch.Cooldowns.Add(new CooldownEntry { Type = type });
                Vm.Save(); RebuildTooltipContent(ch);
            }
        };
        cdAddPanel.Children.Add(cdCombo);
        cdAddPanel.Children.Add(cdAddBtn);
        stack.Children.Add(cdAddPanel);

        // ── Quest Items ──
        stack.Children.Add(new TextBlock { Text = "🏆 Items de quête", FontSize = 10, Foreground = goldBrush, Margin = new Thickness(0, 6, 0, 2) });
        foreach (var qi in ch.QuestItems.ToList())
        {
            var qiRow = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
            var btnQiDel = new Button
            {
                Content = "Retirer", FontSize = 9,
                Padding = new Thickness(5, 1, 5, 1), Margin = new Thickness(6, 0, 0, 0),
                Foreground = Brushes.OrangeRed, Background = new SolidColorBrush(Color.FromRgb(60, 30, 20)),
                BorderBrush = Brushes.OrangeRed, BorderThickness = new Thickness(1)
            };
            btnQiDel.Click += (_, _) => { ch.QuestItems.Remove(qi); Vm.Save(); RebuildTooltipContent(ch); };
            DockPanel.SetDock(btnQiDel, Dock.Right);
            qiRow.Children.Add(btnQiDel);
            qiRow.Children.Add(new TextBlock
            {
                Text = FormatQuestItem(qi.Type), FontSize = 10, Foreground = Brushes.Gold,
                VerticalAlignment = VerticalAlignment.Center
            });
            stack.Children.Add(qiRow);
        }
        var qiAddPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) };
        var qiCombo = new ComboBox { ItemsSource = Enum.GetValues(typeof(QuestItemType)), FontSize = 9, Width = 120, Height = 22 };
        var qiAddBtn = new Button { Content = "+", FontSize = 9, Padding = new Thickness(5, 0, 5, 0), Margin = new Thickness(4, 0, 0, 0) };
        qiAddBtn.Click += (_, _) =>
        {
            if (qiCombo.SelectedItem is QuestItemType type && !ch.QuestItems.Any(q => q.Type == type))
            {
                ch.QuestItems.Add(new QuestItemEntry { Type = type, HasItem = true });
                Vm.Save(); RebuildTooltipContent(ch);
            }
        };
        qiAddPanel.Children.Add(qiCombo);
        qiAddPanel.Children.Add(qiAddBtn);
        stack.Children.Add(qiAddPanel);

        // ── Professions ──
        stack.Children.Add(new TextBlock { Text = "🔨 Métiers", FontSize = 10, Foreground = goldBrush, Margin = new Thickness(0, 6, 0, 2) });
        foreach (var p in ch.Professions)
        {
            stack.Children.Add(new TextBlock
            {
                Text = $"  {p.Type} ({p.Skill}/300)", FontSize = 10, Foreground = dimBrush
            });
        }
        var profAddPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) };
        var profCombo = new ComboBox { ItemsSource = Enum.GetValues(typeof(ProfessionType)), FontSize = 9, Width = 120, Height = 22 };
        var profAddBtn = new Button { Content = "+", FontSize = 9, Padding = new Thickness(5, 0, 5, 0), Margin = new Thickness(4, 0, 0, 0) };
        profAddBtn.Click += (_, _) =>
        {
            if (profCombo.SelectedItem is ProfessionType type && !ch.Professions.Any(pp => pp.Type == type))
            {
                ch.Professions.Add(new ProfessionInfo { Type = type, Skill = 1 });
                Vm.Save(); RebuildTooltipContent(ch);
            }
        };
        profAddPanel.Children.Add(profCombo);
        profAddPanel.Children.Add(profAddBtn);
        stack.Children.Add(profAddPanel);

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
