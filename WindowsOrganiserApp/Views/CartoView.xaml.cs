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
                    if (e.PropertyName is nameof(CartoViewModel.FilteredCharacters))
                    {
                        RedrawMarkers();
                        RebuildCharTree();
                        try { SummaryGrid.Items.Refresh(); } catch { }
                    }
                    else if (e.PropertyName is nameof(CartoViewModel.SelectedCharacter))
                    {
                        RedrawMarkers();
                    }
                    else if (e.PropertyName == nameof(CartoViewModel.Timers))
                    {
                        RedrawTimerMarkers();
                        UpdateTimerCountdowns();
                    }
                    else if (e.PropertyName == nameof(CartoViewModel.OverlayChanged))
                        RedrawAll();
                };
                vm.TimerExpired += OnTimerExpired;
                RedrawAll();
            }
        };
        Loaded += (_, _) =>
        {
            MapImage.SizeChanged += (_, _) =>
            {
                MigrateIfNeeded();
                RedrawAll();
            };
            MapBorder.SizeChanged += (_, _) => CenterMapIfNeeded();
            MigrateIfNeeded();
            RedrawAll();
            CenterMapIfNeeded();
        };
    }

    private double MapWidth => MapImage.ActualWidth > 0 ? MapImage.ActualWidth : 1024;
    private double MapHeight => MapImage.ActualHeight > 0 ? MapImage.ActualHeight : 768;

    private bool _migrated;
    private void MigrateIfNeeded()
    {
        if (_migrated || Vm == null || MapImage.ActualWidth <= 0) return;
        if (Vm.NeedsMigration)
            Vm.MigrateCoordinates(MapImage.ActualWidth, MapImage.ActualHeight);
        _migrated = true;
    }

    private bool _mapCentered;
    private WowCharacter? _draggingCharacter;
    private MapTimer? _draggingTimer;
    private bool _isDragging;

    private void CenterMapIfNeeded()
    {
        if (_mapCentered || MapImage.ActualWidth == 0 || MapBorder.ActualWidth == 0) return;
        _mapCentered = true;
        var zoom = Vm.MapZoom;
        Vm.MapOffsetX = (MapBorder.ActualWidth - MapImage.ActualWidth * zoom) / 2;
        Vm.MapOffsetY = (MapBorder.ActualHeight - MapImage.ActualHeight * zoom) / 2;
    }

    private void RedrawAll()
    {
        RedrawOverlays();
        RedrawMarkers();
        RedrawTimerMarkers();
        RebuildCharTree();
        UpdateTimerCountdowns();
    }

    private void RedrawOverlays()
    {
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

            Brush strokeBrush;
            if (isSelected) strokeBrush = Brushes.White;
            else if (ch.IsExternal) strokeBrush = Brushes.CornflowerBlue;
            else strokeBrush = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0));

            var marker = new Ellipse
            {
                Width = size, Height = size,
                Fill = brush,
                Stroke = strokeBrush,
                StrokeThickness = ch.IsExternal ? 2 : (isSelected ? 2 : 1),
                Cursor = Cursors.Hand,
                Tag = ch
            };

            var pixX = ch.MapX * MapImage.ActualWidth;
            var pixY = ch.MapY * MapImage.ActualHeight;

            Canvas.SetLeft(marker, pixX - size / 2);
            Canvas.SetTop(marker, pixY - size / 2);
            MapCanvas.Children.Add(marker);

            var accountName = Vm.Accounts.FirstOrDefault(a => a.Id == ch.AccountId)?.Name;
            string labelText;
            if (ch.IsExternal && ch.ExternalSource != null)
            {
                var friendName = Vm.GetFriendName(ch.ExternalSource) ?? ch.ExternalSource[..Math.Min(8, ch.ExternalSource.Length)];
                labelText = $"[{friendName}] {ch.Name}";
            }
            else
                labelText = accountName != null ? $"{ch.Name} ({accountName})" : ch.Name;
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
            Canvas.SetLeft(label, pixX - 20);
            Canvas.SetTop(label, pixY - size / 2 - 16);
            MapCanvas.Children.Add(label);
        }
    }

    private void RedrawTimerMarkers()
    {
        if (Vm == null) return;
        for (int i = MapCanvas.Children.Count - 1; i >= 0; i--)
        {
            if (MapCanvas.Children[i] is FrameworkElement fe
                && (fe.Tag is "timer" || fe.Tag is MapTimer))
                MapCanvas.Children.RemoveAt(i);
        }

        foreach (var t in Vm.Timers)
        {
            Brush timerColor;
            if (t.IsRunning) timerColor = Brushes.DeepSkyBlue;
            else if (t.IsPaused) timerColor = Brushes.Gold;
            else timerColor = Brushes.LimeGreen;

            // Draggable ring
            var ring = new Ellipse
            {
                Width = 22, Height = 22,
                Fill = new SolidColorBrush(Color.FromArgb(40, 0, 180, 255)),
                Stroke = timerColor, StrokeThickness = 2.5,
                StrokeDashArray = new DoubleCollection([2, 1]),
                Tag = t, Cursor = Cursors.SizeAll, Opacity = 0.9
            };
            var tPixX = t.MapX * MapImage.ActualWidth;
            var tPixY = t.MapY * MapImage.ActualHeight;
            Canvas.SetLeft(ring, tPixX - 11);
            Canvas.SetTop(ring, tPixY - 11);
            MapCanvas.Children.Add(ring);

            // Label + countdown
            string remaining;
            if (t.IsRunning)
                remaining = FormatTimeSpan((TimeSpan?)t.Remaining);
            else if (t.IsPaused)
                remaining = $"⏸ {FormatTimeSpan((TimeSpan?)t.Remaining)}";
            else
                remaining = "⏹";

            var labelPanel = new StackPanel { Orientation = Orientation.Horizontal, Tag = "timer" };
            labelPanel.Children.Add(new TextBlock
            {
                Text = $"⏱ {t.Label}: {remaining}",
                FontSize = 8, Foreground = timerColor, VerticalAlignment = VerticalAlignment.Center
            });

            // Action buttons on map (contextual)
            Button MakeMapBtn(string text, Brush fg, Action action) {
                var b = new Button {
                    Content = text, FontSize = 9, Padding = new Thickness(3, 0, 3, 0),
                    Margin = new Thickness(3, 0, 0, 0), Cursor = Cursors.Hand,
                    Foreground = fg, Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0), Tag = t,
                    MinWidth = 0, MinHeight = 0
                };
                b.Click += (_, _) => { action(); RedrawAll(); };
                return b;
            }

            if (t.IsRunning)
            {
                labelPanel.Children.Add(MakeMapBtn("⏸", Brushes.Gold,
                    () => Vm.StopTimerCommand.Execute(t)));
            }
            else if (t.IsPaused)
            {
                labelPanel.Children.Add(MakeMapBtn("▶", Brushes.LimeGreen,
                    () => Vm.ResumeTimerCommand.Execute(t)));
            }

            labelPanel.Children.Add(MakeMapBtn("↻", Brushes.LightSkyBlue,
                () => Vm.RestartTimerCommand.Execute(t)));
            labelPanel.Children.Add(MakeMapBtn("✕", Brushes.OrangeRed,
                () => Vm.RemoveTimerCommand.Execute(t)));

            var timerLabel = new Border
            {
                Tag = "timer", Child = labelPanel,
                Background = new SolidColorBrush(Color.FromArgb(220, 10, 10, 10)),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(5, 2, 5, 2)
            };
            Canvas.SetLeft(timerLabel, tPixX - 30);
            Canvas.SetTop(timerLabel, tPixY + 13);
            MapCanvas.Children.Add(timerLabel);
        }
    }

    private void UpdateTimerCountdowns()
    {
        if (TimerList == null) return;
        foreach (var container in TimerList.Items.Cast<object>()
            .Select((_, i) => TimerList.ItemContainerGenerator.ContainerFromIndex(i))
            .OfType<ContentPresenter>())
        {
            var timer = container.Content as MapTimer;
            if (timer == null) continue;

            var tb = FindVisualChildren<TextBlock>(container)
                .FirstOrDefault(t => t.Tag == timer);
            if (tb != null)
            {
                if (timer.IsExpired && !timer.IsRunning)
                {
                    tb.Text = "Terminé";
                    tb.Foreground = new SolidColorBrush(Color.FromRgb(100, 200, 100));
                    tb.FontSize = 10;
                    tb.FontWeight = FontWeights.Normal;
                }
                else if (timer.IsRunning)
                {
                    tb.Text = FormatTimeSpan((TimeSpan?)timer.Remaining);
                    tb.Foreground = Brushes.DeepSkyBlue;
                    tb.FontSize = 12;
                    tb.FontWeight = FontWeights.Bold;
                }
                else
                {
                    tb.Text = "⏸ Pause";
                    tb.Foreground = Brushes.Gold;
                    tb.FontSize = 10;
                    tb.FontWeight = FontWeights.Normal;
                }
            }

            UpdateTimerCardStyle(container, timer);
        }
    }

    private void UpdateTimerCardStyle(ContentPresenter container, MapTimer timer)
    {
        var card = FindVisualChildren<Border>(container)
            .FirstOrDefault(b => b.Name == "TimerProgressBar" || b.Tag is "timerCard");

        var outerBorder = FindVisualChildren<Border>(container).FirstOrDefault();
        if (outerBorder == null) return;

        Color accent;
        if (timer.IsExpired && !timer.IsRunning)
            accent = Color.FromRgb(80, 200, 80);
        else if (timer.IsRunning)
            accent = Color.FromRgb(0, 180, 255);
        else
            accent = Color.FromRgb(255, 215, 0);

        outerBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(80, accent.R, accent.G, accent.B));

        var progressBar = FindVisualChildren<Border>(container)
            .FirstOrDefault(b => b.Name == "TimerProgressBar");
        if (progressBar != null && outerBorder.ActualWidth > 0)
        {
            double pct = 0;
            if (timer.IsRunning && timer.DurationSeconds > 0)
                pct = Math.Clamp(timer.Remaining.TotalSeconds / timer.DurationSeconds, 0, 1);
            else if (timer.IsExpired)
                pct = 1;

            progressBar.Width = outerBorder.ActualWidth * pct;
            progressBar.Background = new SolidColorBrush(accent);
            progressBar.Opacity = 0.12;
        }
    }

    private void TimerCard_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is Border border && border.DataContext is MapTimer timer)
        {
            Color accent;
            if (timer.IsExpired && !timer.IsRunning)
                accent = Color.FromRgb(80, 200, 80);
            else if (timer.IsRunning)
                accent = Color.FromRgb(0, 180, 255);
            else
                accent = Color.FromRgb(255, 215, 0);
            border.BorderBrush = new SolidColorBrush(Color.FromArgb(80, accent.R, accent.G, accent.B));
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T t) yield return t;
            foreach (var sub in FindVisualChildren<T>(child)) yield return sub;
        }
    }

    private void MapCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (Vm.IsPlacingTimer)
        {
            var pos = e.GetPosition(MapImage);
            Vm.PlaceTimerAt(pos.X / MapImage.ActualWidth, pos.Y / MapImage.ActualHeight);
            RedrawAll();
            e.Handled = true;
            return;
        }

        if (Vm.IsPlacingCharacter)
        {
            var pos = e.GetPosition(MapImage);
            Vm.PlaceCharacterAt(pos.X / MapImage.ActualWidth, pos.Y / MapImage.ActualHeight);
            RedrawAll();
            e.Handled = true;
            return;
        }

        // Marker click: drag or tooltip toggle
        if (e.OriginalSource is Ellipse { Tag: WowCharacter ch })
        {
            _draggingCharacter = ch;
            _isDragging = false;
            _panStart = e.GetPosition(MapBorder);
            MapBorder.CaptureMouse();
            e.Handled = true;
            return;
        }

        // Timer ring: drag
        if (e.OriginalSource is Ellipse { Tag: MapTimer timer })
        {
            _draggingTimer = timer;
            _isDragging = false;
            _panStart = e.GetPosition(MapBorder);
            MapBorder.CaptureMouse();
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
        else if (Vm.IsPlacingTimer)
        {
            Vm.IsPlacingTimer = false;
            e.Handled = true;
        }
    }

    private void MapCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(MapBorder);

        // Drag character marker
        if (_draggingCharacter != null)
        {
            var delta = pos - _panStart;
            if (!_isDragging && (Math.Abs(delta.X) > 4 || Math.Abs(delta.Y) > 4))
                _isDragging = true;

            if (_isDragging)
            {
                var mapPos = e.GetPosition(MapImage);
                _draggingCharacter.MapX = Math.Clamp(mapPos.X / MapWidth, 0, 1);
                _draggingCharacter.MapY = Math.Clamp(mapPos.Y / MapHeight, 0, 1);
                RedrawMarkers();
            }
            return;
        }

        // Drag timer
        if (_draggingTimer != null)
        {
            var delta = pos - _panStart;
            if (!_isDragging && (Math.Abs(delta.X) > 4 || Math.Abs(delta.Y) > 4))
                _isDragging = true;

            if (_isDragging)
            {
                var mapPos = e.GetPosition(MapImage);
                _draggingTimer.MapX = Math.Clamp(mapPos.X / MapWidth, 0, 1);
                _draggingTimer.MapY = Math.Clamp(mapPos.Y / MapHeight, 0, 1);
                RedrawTimerMarkers();
            }
            return;
        }

        if (!_isPanning) return;
        Vm.MapOffsetX = _panStartOffsetX + (pos.X - _panStart.X);
        Vm.MapOffsetY = _panStartOffsetY + (pos.Y - _panStart.Y);
    }

    private void MapCanvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        // End timer drag
        if (_draggingTimer != null)
        {
            if (_isDragging)
                Vm.Save();
            _draggingTimer = null;
            _isDragging = false;
            MapBorder.ReleaseMouseCapture();
            RedrawAll();
            e.Handled = true;
            return;
        }

        // End character drag
        if (_draggingCharacter != null)
        {
            if (_isDragging)
            {
                Vm.Save();
                RedrawAll();
            }
            else
            {
                var ch = _draggingCharacter;
                if (_tooltipCharacter == ch && CharPopup.IsOpen)
                    CloseCharacterTooltip();
                else
                {
                    Vm.SelectedCharacter = ch;
                    RedrawAll();
                    ShowCharacterTooltip(ch);
                }
            }
            _draggingCharacter = null;
            _isDragging = false;
            MapBorder.ReleaseMouseCapture();
            e.Handled = true;
            return;
        }

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

    private void RebuildCharTree()
    {
        if (Vm == null) return;
        CharTreeView.Items.Clear();

        var grouped = Vm.Characters
            .Where(c => !c.IsExternal)
            .GroupBy(c => c.AccountId ?? "__none__");

        foreach (var group in grouped)
        {
            var account = Vm.Accounts.FirstOrDefault(a => a.Id == group.Key);
            var accountName = account?.Name ?? "Sans compte";
            var visibleCount = group.Count(c => !c.IsHidden);
            var totalCount = group.Count();

            var headerPanel = new DockPanel();
            if (account != null)
            {
                var toggleBtn = new Button
                {
                    Content = account.IsHidden ? "🚫" : "👁",
                    FontSize = 9, Padding = new Thickness(3, 0, 3, 0),
                    Margin = new Thickness(4, 0, 0, 0), Cursor = Cursors.Hand,
                    Foreground = account.IsHidden ? Brushes.Gray : Brushes.LightGreen,
                    Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                    ToolTip = account.IsHidden ? "Afficher le compte" : "Masquer le compte"
                };
                var capturedAccount = account;
                toggleBtn.Click += (_, _) =>
                {
                    Vm.ToggleAccountVisibilityCommand.Execute(capturedAccount);
                    toggleBtn.Content = capturedAccount.IsHidden ? "🚫" : "👁";
                    toggleBtn.Foreground = capturedAccount.IsHidden ? Brushes.Gray : Brushes.LightGreen;
                    toggleBtn.ToolTip = capturedAccount.IsHidden ? "Afficher le compte" : "Masquer le compte";
                };
                DockPanel.SetDock(toggleBtn, Dock.Right);
                headerPanel.Children.Add(toggleBtn);
            }
            headerPanel.Children.Add(new TextBlock
            {
                Text = $"👤 {accountName} ({visibleCount}/{totalCount})",
                FontSize = 11, FontWeight = FontWeights.SemiBold,
                Foreground = account?.IsHidden == true ? Brushes.Gray : Brushes.Gold,
                VerticalAlignment = VerticalAlignment.Center
            });

            var parentItem = new TreeViewItem
            {
                Header = headerPanel,
                IsExpanded = true,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold
            };

            foreach (var ch in group.OrderByDescending(c => c.Level))
                parentItem.Items.Add(BuildCharTreeItem(ch, false));

            CharTreeView.Items.Add(parentItem);
        }

        var externals = Vm.FilteredCharacters.Where(c => c.IsExternal).ToList();
        if (externals.Count > 0)
        {
            var byFriend = externals.GroupBy(c => c.ExternalSource ?? "?");
            foreach (var friendGroup in byFriend)
            {
                var friendName = Vm.GetFriendName(friendGroup.Key)
                    ?? friendGroup.Key[..Math.Min(8, friendGroup.Key.Length)];
                var friendItem = new TreeViewItem
                {
                    Header = $"🌐 {friendName} ({friendGroup.Count()})",
                    IsExpanded = false,
                    Foreground = Brushes.CornflowerBlue,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold
                };
                foreach (var ch in friendGroup.OrderByDescending(c => c.Level))
                    friendItem.Items.Add(BuildCharTreeItem(ch, true));
                CharTreeView.Items.Add(friendItem);
            }
        }
    }

    private TreeViewItem BuildCharTreeItem(WowCharacter ch, bool isExternal)
    {
        var classHex = WowClassColors.GetHexColor(ch.Class);
        var classBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(classHex));

        var dock = new DockPanel();

        if (!isExternal)
        {
            var toggleBtn = new Button
            {
                Content = ch.IsHidden ? "🚫" : "👁",
                FontSize = 8, Padding = new Thickness(2, 0, 2, 0),
                Margin = new Thickness(4, 0, 0, 0), Cursor = Cursors.Hand,
                Foreground = ch.IsHidden ? Brushes.Gray : Brushes.LightGreen,
                Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                ToolTip = ch.IsHidden ? "Afficher" : "Masquer"
            };
            var capturedCh = ch;
            toggleBtn.Click += (_, _) =>
            {
                Vm.ToggleCharacterVisibilityCommand.Execute(capturedCh);
                toggleBtn.Content = capturedCh.IsHidden ? "🚫" : "👁";
                toggleBtn.Foreground = capturedCh.IsHidden ? Brushes.Gray : Brushes.LightGreen;
                toggleBtn.ToolTip = capturedCh.IsHidden ? "Afficher" : "Masquer";
            };
            DockPanel.SetDock(toggleBtn, Dock.Right);
            dock.Children.Add(toggleBtn);
        }

        var panel = new StackPanel { Orientation = Orientation.Horizontal };

        panel.Children.Add(new Ellipse
        {
            Width = 8, Height = 8,
            Fill = ch.IsHidden ? Brushes.Gray : classBrush,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0)
        });

        var prefix = "";
        if (isExternal && ch.ExternalSource != null)
        {
            var fn = Vm.GetFriendName(ch.ExternalSource) ?? ch.ExternalSource[..Math.Min(8, ch.ExternalSource.Length)];
            prefix = $"[{fn}] ";
        }
        panel.Children.Add(new TextBlock
        {
            Text = $"{prefix}{ch.Name}", FontSize = 10,
            Foreground = ch.IsHidden ? Brushes.Gray : Brushes.White,
            VerticalAlignment = VerticalAlignment.Center
        });

        panel.Children.Add(new TextBlock
        {
            Text = $"  {ch.Class}", FontSize = 9,
            Foreground = ch.IsHidden ? Brushes.DarkGray : classBrush,
            FontStyle = FontStyles.Italic,
            VerticalAlignment = VerticalAlignment.Center
        });

        panel.Children.Add(new TextBlock
        {
            Text = $"  Lv.{ch.Level}", FontSize = 9,
            Foreground = new SolidColorBrush(ch.IsHidden ? Color.FromRgb(100, 100, 100) : Color.FromRgb(180, 180, 180)),
            VerticalAlignment = VerticalAlignment.Center
        });

        panel.Children.Add(new TextBlock
        {
            Text = $"  [{ch.Status.DisplayName()}]", FontSize = 8,
            Foreground = new SolidColorBrush(Color.FromRgb(140, 140, 140)),
            FontStyle = FontStyles.Italic,
            VerticalAlignment = VerticalAlignment.Center
        });

        if (ch.MapX == 0 && ch.MapY == 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text = "  ⚠ non placé", FontSize = 8,
                Foreground = Brushes.OrangeRed, VerticalAlignment = VerticalAlignment.Center
            });
        }

        dock.Children.Add(panel);

        return new TreeViewItem
        {
            Header = dock, Tag = ch,
            FontSize = 10, FontWeight = FontWeights.Normal,
            Opacity = ch.IsHidden ? 0.5 : 1.0
        };
    }

    private void CharTreeView_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (CharTreeView.SelectedItem is TreeViewItem { Tag: WowCharacter ch })
            ShowCharacterTooltip(ch);
    }

    private static readonly System.Media.SoundPlayer _chimePlayer = new(@"C:\Windows\Media\chimes.wav");

    private void OnTimerExpired(MapTimer t)
    {
        try { _chimePlayer.Play(); } catch { }
        RedrawAll();
    }

    private void TimerRestart_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: MapTimer t })
        { Vm.RestartTimerCommand.Execute(t); RedrawAll(); }
    }

    private void TimerResume_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: MapTimer t })
        { Vm.ResumeTimerCommand.Execute(t); RefreshTimerListButtons(); RedrawAll(); }
    }

    private void TimerStop_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: MapTimer t })
        { Vm.StopTimerCommand.Execute(t); RefreshTimerListButtons(); RedrawAll(); }
    }

    private void TimerRemove_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: MapTimer t })
        { Vm.RemoveTimerCommand.Execute(t); RedrawAll(); }
    }

    private void TimerPlayPauseBtn_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is MapTimer t)
        {
            var content = btn.Content as string;
            if (content == "▶") btn.Visibility = t.IsRunning ? Visibility.Collapsed : Visibility.Visible;
            else if (content == "⏸") btn.Visibility = t.IsRunning ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void RefreshTimerListButtons()
    {
        foreach (var cp in Enumerable.Range(0, TimerList.Items.Count)
            .Select(i => TimerList.ItemContainerGenerator.ContainerFromIndex(i))
            .OfType<ContentPresenter>())
        {
            if (cp.Content is not MapTimer t) continue;
            foreach (var btn in FindVisualChildren<Button>(cp))
            {
                var content = btn.Content as string;
                if (content == "▶") btn.Visibility = t.IsRunning ? Visibility.Collapsed : Visibility.Visible;
                else if (content == "⏸") btn.Visibility = t.IsRunning ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }

    private void TimerDurationPanel_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not StackPanel sp || sp.DataContext is not MapTimer t) return;
        var ts = TimeSpan.FromSeconds(t.DurationSeconds);
        foreach (var box in sp.Children.OfType<TextBox>())
        {
            switch (box.Tag as string)
            {
                case "h": box.Text = ((int)ts.TotalHours).ToString(); break;
                case "m": box.Text = ts.Minutes.ToString(); break;
                case "s": box.Text = ts.Seconds.ToString(); break;
            }
        }
    }

    private void TimerDuration_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox tb || tb.DataContext is not MapTimer t) return;
        var parent = tb.Parent as StackPanel;
        if (parent == null) return;

        var boxes = parent.Children.OfType<TextBox>().ToList();
        int h = 0, m = 0, s = 0;
        foreach (var box in boxes)
        {
            int.TryParse(box.Text, out var val);
            switch (box.Tag as string)
            {
                case "h": h = val; break;
                case "m": m = val; break;
                case "s": s = val; break;
            }
        }
        var total = h * 3600 + m * 60 + s;
        if (total <= 0) return;

        t.DurationSeconds = total;
        t.IsRunning = false;
        t.StartedAt = null;
        Vm.Save();
        RedrawAll();
    }

    private void FriendVisibilityBtn_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is Models.Carto.FriendEntry friend)
            btn.Content = friend.IsVisible ? "👁" : "🚫";
    }

    private void FriendVisibility_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is Models.Carto.FriendEntry friend)
            btn.Content = friend.IsVisible ? "👁" : "🚫";
    }

    private void ActionChar_Click(object sender, RoutedEventArgs e) => PopupAddChar.IsOpen = true;
    private void ActionAccount_Click(object sender, RoutedEventArgs e) => PopupAddAccount.IsOpen = true;
    private void ActionTimer_Click(object sender, RoutedEventArgs e) => PopupAddTimer.IsOpen = true;
    private void ActionFriend_Click(object sender, RoutedEventArgs e) => PopupAddFriend.IsOpen = true;

    private void PopupClose_Click(object sender, RoutedEventArgs e)
    {
        PopupAddChar.IsOpen = false;
        PopupAddAccount.IsOpen = false;
        PopupAddTimer.IsOpen = false;
        PopupAddFriend.IsOpen = false;
    }

    private void PopupAddAccount_Click(object sender, RoutedEventArgs e) => DoAddAccountFromPopup();

    private void PopupNewAccountBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Return)
        {
            DoAddAccountFromPopup();
            e.Handled = true;
        }
    }

    private void DoAddAccountFromPopup()
    {
        var name = PopupNewAccountBox.Text.Trim();
        if (!string.IsNullOrEmpty(name))
        {
            Vm.AddAccountCommand.Execute(name);
            PopupNewAccountBox.Text = string.Empty;
        }
    }

    private void RemoveAccount_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: WowAccount account })
            Vm.RemoveAccountCommand.Execute(account);
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
        var goldBrush = new SolidColorBrush(Color.FromRgb(218, 165, 32));
        var dimBrush = new SolidColorBrush(Color.FromRgb(160, 155, 140));
        var bgInput = new SolidColorBrush(Color.FromRgb(30, 26, 18));
        var sectionBorder = new SolidColorBrush(Color.FromRgb(80, 65, 30));

        // ═══ HEADER BANNER ═══
        var banner = new Border
        {
            Background = new LinearGradientBrush(
                Color.FromArgb(220, 40, 32, 15), Color.FromArgb(180, 20, 16, 8), 0),
            BorderBrush = classBrush, BorderThickness = new Thickness(0, 0, 0, 2),
            CornerRadius = new CornerRadius(4, 4, 0, 0),
            Padding = new Thickness(10, 8, 10, 8), Margin = new Thickness(-10, -10, -10, 8)
        };
        var bannerStack = new StackPanel();

        // Class icon + editable name
        var nameRow = new DockPanel();
        nameRow.Children.Add(new Ellipse
        {
            Width = 14, Height = 14, Fill = classBrush,
            Stroke = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)),
            StrokeThickness = 1, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        });
        var nameBox = new TextBox
        {
            Text = ch.Name, FontSize = 14, FontWeight = FontWeights.Bold,
            Background = Brushes.Transparent, Foreground = classBrush,
            BorderThickness = new Thickness(0, 0, 0, 1),
            BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 215, 0)),
            Padding = new Thickness(2, 0, 2, 2), CaretBrush = Brushes.White
        };
        nameBox.LostFocus += (_, _) => { ch.Name = nameBox.Text; Vm.Save(); RedrawAll(); };
        nameRow.Children.Add(nameBox);
        bannerStack.Children.Add(nameRow);

        // Class + level + account row
        var infoRow = new WrapPanel { Margin = new Thickness(22, 4, 0, 0) };
        var classCombo = new ComboBox
        {
            ItemsSource = Enum.GetValues(typeof(WowClass)),
            SelectedItem = ch.Class,
            FontSize = 9, Height = 20, MinWidth = 75,
            Background = bgInput, Foreground = classBrush,
            BorderBrush = sectionBorder, BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 8, 0)
        };
        classCombo.SelectionChanged += (_, _) =>
        {
            if (classCombo.SelectedItem is WowClass wc)
            { ch.Class = wc; Vm.Save(); RebuildTooltipContent(ch); RedrawAll(); }
        };
        infoRow.Children.Add(classCombo);
        infoRow.Children.Add(new TextBlock
        {
            Text = "Lv.", FontSize = 9, Foreground = dimBrush,
            VerticalAlignment = VerticalAlignment.Center
        });
        var lvlBox = new TextBox
        {
            Text = ch.Level.ToString(), FontSize = 10, Width = 28,
            Background = Brushes.Transparent, Foreground = Brushes.White,
            BorderThickness = new Thickness(0, 0, 0, 1),
            BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
            Padding = new Thickness(2, 0, 2, 1), Margin = new Thickness(2, 0, 10, 0),
            TextAlignment = TextAlignment.Center, CaretBrush = Brushes.White
        };
        lvlBox.LostFocus += (_, _) =>
        {
            if (int.TryParse(lvlBox.Text, out var lv) && lv is >= 1 and <= 60)
                ch.Level = lv;
            else
                lvlBox.Text = ch.Level.ToString();
            Vm.Save(); RedrawAll();
        };
        infoRow.Children.Add(lvlBox);

        var accountCombo = new ComboBox
        {
            ItemsSource = Vm.Accounts, DisplayMemberPath = "Name",
            SelectedValuePath = "Id", SelectedValue = ch.AccountId,
            FontSize = 9, Height = 20, MinWidth = 80,
            Background = bgInput, Foreground = Brushes.White,
            BorderBrush = sectionBorder, BorderThickness = new Thickness(1)
        };
        accountCombo.SelectionChanged += (_, _) =>
        {
            if (accountCombo.SelectedValue is string id)
            { ch.AccountId = id; Vm.Save(); RedrawAll(); }
        };
        infoRow.Children.Add(accountCombo);

        var statusCombo = new ComboBox
        {
            FontSize = 9, Height = 20, MinWidth = 70,
            Background = bgInput, Foreground = Brushes.White,
            BorderBrush = sectionBorder, BorderThickness = new Thickness(1),
            Margin = new Thickness(4, 0, 0, 0)
        };
        foreach (var s in Enum.GetValues(typeof(CharacterStatus)).Cast<CharacterStatus>())
        {
            var item = new ComboBoxItem { Content = s.DisplayName(), Tag = s };
            if (s == ch.Status) item.IsSelected = true;
            statusCombo.Items.Add(item);
        }
        statusCombo.SelectionChanged += (_, _) =>
        {
            if (statusCombo.SelectedItem is ComboBoxItem { Tag: CharacterStatus s })
            { ch.Status = s; Vm.Save(); RedrawAll(); }
        };
        infoRow.Children.Add(statusCombo);

        bannerStack.Children.Add(infoRow);
        banner.Child = bannerStack;
        stack.Children.Add(banner);

        // ═══ QUEST ITEMS (display at top — important info) ═══
        if (ch.QuestItems.Count > 0)
        {
            var qiTopPanel = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
            foreach (var qi in ch.QuestItems.ToList())
            {
                var badge = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(50, 255, 215, 0)),
                    BorderBrush = Brushes.Goldenrod, BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 2, 6, 2),
                    Margin = new Thickness(0, 0, 4, 3)
                };
                var badgeDock = new DockPanel();
                var btnQiDel = MakeSmallButton("✕", Brushes.OrangeRed,
                    () => { ch.QuestItems.Remove(qi); Vm.Save(); RebuildTooltipContent(ch); });
                DockPanel.SetDock(btnQiDel, Dock.Right);
                badgeDock.Children.Add(btnQiDel);
                badgeDock.Children.Add(new TextBlock
                {
                    Text = $"🏆 {FormatQuestItem(qi.Type)}", FontSize = 10,
                    Foreground = Brushes.Gold, FontWeight = FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center
                });
                badge.Child = badgeDock;
                qiTopPanel.Children.Add(badge);
            }
            stack.Children.Add(qiTopPanel);
        }

        // ═══ SHARDS (Warlock) ═══
        if (ch.Class == WowClass.Demoniste)
        {
            var shardSection = MakeSection("💎 Shards", new SolidColorBrush(Color.FromRgb(148, 130, 201)));
            var shardBox = new TextBox
            {
                Text = ch.ShardCount.ToString(), Width = 50, FontSize = 11,
                Background = bgInput, Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(148, 130, 201)),
                BorderThickness = new Thickness(1), Padding = new Thickness(4, 2, 4, 2),
                TextAlignment = TextAlignment.Center, CaretBrush = Brushes.White
            };
            shardBox.LostFocus += (_, _) =>
            {
                if (int.TryParse(shardBox.Text, out var val) && val >= 0)
                    ch.ShardCount = val;
                else
                    shardBox.Text = ch.ShardCount.ToString();
                Vm.Save();
            };
            ((StackPanel)shardSection.Child).Children.Add(shardBox);
            stack.Children.Add(shardSection);
        }

        // ═══ PROFESSIONS (2 combos côte à côte) ═══
        ProfessionType[] excludedProfs = [ProfessionType.Peche, ProfessionType.Cuisine, ProfessionType.Secourisme];
        var profItems = new List<object> { "Aucun" };
        profItems.AddRange(Enum.GetValues(typeof(ProfessionType)).Cast<ProfessionType>()
            .Where(pt => !excludedProfs.Contains(pt)).Cast<object>());

        var profSection = MakeSection("🔨 Métiers", goldBrush);
        var profPanel = new StackPanel { Orientation = Orientation.Horizontal };

        for (int i = 0; i < 2; i++)
        {
            var idx = i;
            object current = ch.Professions.Count > idx ? ch.Professions[idx].Type : "Aucun";
            var combo = new ComboBox
            {
                ItemsSource = profItems, FontSize = 9, Width = 120, Height = 22,
                SelectedItem = current,
                Background = bgInput, Foreground = Brushes.White,
                BorderBrush = sectionBorder, BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 6, 0)
            };
            combo.SelectionChanged += (_, _) =>
            {
                if (combo.SelectedItem is ProfessionType type)
                {
                    if (ch.Professions.Count > idx)
                        ch.Professions[idx] = new ProfessionInfo { Type = type };
                    else
                        ch.Professions.Add(new ProfessionInfo { Type = type });
                }
                else if (combo.SelectedItem is string && ch.Professions.Count > idx)
                {
                    ch.Professions.RemoveAt(idx);
                }
                Vm.Save(); RebuildTooltipContent(ch);
            };
            profPanel.Children.Add(combo);
        }
        ((StackPanel)profSection.Child).Children.Add(profPanel);
        stack.Children.Add(profSection);

        // ═══ COOLDOWNS ═══
        var cdSection = MakeSection("⏱ Cooldowns", goldBrush);
        var cdStack = (StackPanel)cdSection.Child;
        foreach (var cd in ch.Cooldowns.ToList())
        {
            var cdRow = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(40, 255, 200, 0)),
                CornerRadius = new CornerRadius(3), Padding = new Thickness(6, 3, 6, 3),
                Margin = new Thickness(0, 0, 0, 3)
            };
            var cdDock = new DockPanel();
            var btnDel = MakeSmallButton("✕", Brushes.IndianRed,
                () => { ch.Cooldowns.Remove(cd); Vm.Save(); RebuildTooltipContent(ch); });
            var isRunning = cd.LastUsed != null && !cd.IsReady;
            var btnAct = MakeSmallButton(isRunning ? "⏸" : "▶", isRunning ? Brushes.Gold : Brushes.LightSkyBlue,
                () =>
                {
                    if (cd.LastUsed != null && !cd.IsReady)
                        cd.LastUsed = null;
                    else
                        cd.LastUsed = DateTime.Now;
                    cd.Note = null;
                    Vm.Save();
                    RebuildTooltipContent(ch);
                });
            DockPanel.SetDock(btnDel, Dock.Right);
            DockPanel.SetDock(btnAct, Dock.Right);
            cdDock.Children.Add(btnDel);
            cdDock.Children.Add(btnAct);

            string status;
            Brush statusColor;
            if (cd.LastUsed == null) { status = "—"; statusColor = Brushes.Gray; }
            else if (cd.IsReady) { status = "✅ PRÊT"; statusColor = Brushes.LightGreen; }
            else { status = $"▶ {FormatTimeSpan(cd.TimeRemaining)}"; statusColor = Brushes.DeepSkyBlue; }
            cdDock.Children.Add(new TextBlock
            {
                Text = $"{cd.Type.DisplayName()}: {status}", FontSize = 10,
                Foreground = statusColor, VerticalAlignment = VerticalAlignment.Center
            });
            cdRow.Child = cdDock;
            cdStack.Children.Add(cdRow);
        }
        var availableCds = GetAvailableCooldowns(ch);
        var cdAddPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 0) };
        var cdCombo = new ComboBox
        {
            FontSize = 9, Width = 180, Height = 22,
            Background = bgInput, Foreground = Brushes.White, BorderBrush = sectionBorder
        };
        foreach (var ct in availableCds)
            cdCombo.Items.Add(new ComboBoxItem { Content = ct.DisplayName(), Tag = ct });
        var cdAddBtn = MakeSmallButton("+", Brushes.LightGreen,
            () => { if (cdCombo.SelectedItem is ComboBoxItem { Tag: CooldownType type } && !ch.Cooldowns.Any(c => c.Type == type))
            { ch.Cooldowns.Add(new CooldownEntry { Type = type }); Vm.Save(); RebuildTooltipContent(ch); } });
        cdAddPanel.Children.Add(cdCombo);
        cdAddPanel.Children.Add(cdAddBtn);
        cdStack.Children.Add(cdAddPanel);
        stack.Children.Add(cdSection);

        // ═══ QUEST ITEMS (add only) ═══
        var qiSection = MakeSection("🏆 Ajouter item de quête", goldBrush);
        var qiStack = (StackPanel)qiSection.Child;
        var qiAddPanel = new StackPanel { Orientation = Orientation.Horizontal };
        var qiCombo = new ComboBox
        {
            ItemsSource = Enum.GetValues(typeof(QuestItemType)), FontSize = 9, Width = 130, Height = 22,
            Background = bgInput, Foreground = Brushes.White, BorderBrush = sectionBorder
        };
        var qiAddBtn = MakeSmallButton("+", Brushes.LightGreen,
            () => { if (qiCombo.SelectedItem is QuestItemType type && !ch.QuestItems.Any(q => q.Type == type))
            { ch.QuestItems.Add(new QuestItemEntry { Type = type, HasItem = true }); Vm.Save(); RebuildTooltipContent(ch); } });
        qiAddPanel.Children.Add(qiCombo);
        qiAddPanel.Children.Add(qiAddBtn);
        qiStack.Children.Add(qiAddPanel);
        stack.Children.Add(qiSection);

        // ═══ NOTE ═══
        var noteSection = MakeSection("📝 Note", goldBrush);
        var noteBox = new TextBox
        {
            Text = ch.Note ?? "", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
            MinHeight = 32, MaxHeight = 70, FontSize = 10,
            Background = bgInput, Foreground = Brushes.White,
            BorderBrush = sectionBorder, BorderThickness = new Thickness(1),
            Padding = new Thickness(6, 4, 6, 4), CaretBrush = Brushes.White
        };
        noteBox.LostFocus += (_, _) => { ch.Note = noteBox.Text; Vm.Save(); };
        ((StackPanel)noteSection.Child).Children.Add(noteBox);
        stack.Children.Add(noteSection);

        // ═══ ACTION BUTTONS (bas du tooltip) ═══
        var actionsPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        actionsPanel.Children.Add(MakeActionButton("🗑 Suppr", "#FFCC3333", () =>
        {
            if (MessageBox.Show($"Supprimer \"{ch.Name}\" ?", "Confirmation",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            { CloseCharacterTooltip(); Vm.RemoveCharacterCommand.Execute(ch); RedrawAll(); }
        }));
        actionsPanel.Children.Add(MakeActionButton("✓ Fermer", "#FF4A8C3F", CloseCharacterTooltip));
        stack.Children.Add(actionsPanel);
    }

    private static Border MakeSection(string title, Brush titleBrush)
    {
        var border = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(60, 50, 25)),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(Color.FromArgb(30, 255, 215, 0)),
            Padding = new Thickness(8, 6, 8, 6), Margin = new Thickness(0, 0, 0, 6)
        };
        var sp = new StackPanel();
        sp.Children.Add(new TextBlock
        {
            Text = title, FontSize = 10, FontWeight = FontWeights.SemiBold,
            Foreground = titleBrush, Margin = new Thickness(0, 0, 0, 4)
        });
        border.Child = sp;
        return border;
    }

    private static Button MakeSmallButton(string content, Brush fg, Action onClick)
    {
        var btn = new Button
        {
            Content = content, FontSize = 9, Padding = new Thickness(4, 1, 4, 1),
            Margin = new Thickness(3, 0, 0, 0), Foreground = fg,
            Background = new SolidColorBrush(Color.FromRgb(35, 30, 20)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(70, 60, 40)),
            BorderThickness = new Thickness(1), Cursor = Cursors.Hand
        };
        btn.Click += (_, _) => onClick();
        return btn;
    }

    private static Button MakeActionButton(string content, string colorHex, Action onClick)
    {
        var color = (Color)ColorConverter.ConvertFromString(colorHex);
        var btn = new Button
        {
            Content = content, FontSize = 9, FontWeight = FontWeights.SemiBold,
            Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(0, 0, 6, 0),
            Foreground = new SolidColorBrush(color),
            Background = new SolidColorBrush(Color.FromArgb(40, color.R, color.G, color.B)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(120, color.R, color.G, color.B)),
            BorderThickness = new Thickness(1), Cursor = Cursors.Hand
        };
        btn.Click += (_, _) => onClick();
        return btn;
    }

    private static TextBlock MakeLabel(string text, int row, int col, Brush fg)
    {
        var tb = new TextBlock
        {
            Text = text, FontSize = 10, Foreground = fg,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 2)
        };
        Grid.SetRow(tb, row);
        Grid.SetColumn(tb, col);
        return tb;
    }

    private static string FormatTimeSpan(TimeSpan? ts)
    {
        if (ts == null) return "—";
        if (ts.Value.TotalHours >= 1)
            return $"{(int)ts.Value.TotalHours}h{ts.Value.Minutes:D2}";
        if (ts.Value.TotalSeconds < 60)
            return $"{ts.Value.Seconds}s";
        return $"{(int)ts.Value.TotalMinutes}m{ts.Value.Seconds:D2}s";
    }

    private static CooldownType[] GetAvailableCooldowns(WowCharacter ch)
    {
        var profs = ch.Professions.Select(p => p.Type).ToHashSet();
        var result = new List<CooldownType>();

        if (profs.Contains(ProfessionType.Alchimie))
            result.AddRange([CooldownType.Arcanite, CooldownType.Transmute_Elementaire]);
        if (profs.Contains(ProfessionType.Couture))
            result.Add(CooldownType.Mooncloth);
        if (profs.Contains(ProfessionType.Travail_du_cuir))
            result.Add(CooldownType.Sel_raffine);

        return result.Where(ct => !ch.Cooldowns.Any(c => c.Type == ct)).ToArray();
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
