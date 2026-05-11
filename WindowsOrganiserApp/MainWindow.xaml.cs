using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using WindowsOrganiserApp.Models;
using WindowsOrganiserApp.Services;
using WindowsOrganiserApp.ViewModels;

namespace WindowsOrganiserApp;

public partial class MainWindow : Window
{
    private Point _dragStartPoint;
    private DropIndicatorAdorner? _dropAdorner;

    private readonly MainViewModel _viewModel;
    private readonly ISettingsService _settingsService;

    public MainWindow(MainViewModel viewModel, IWindowService windowService, ISettingsService settingsService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _settingsService = settingsService;
        DataContext = viewModel;

        Loaded += (_, _) =>
        {
            windowService.OwnHandle = new WindowInteropHelper(this).Handle;
            RestoreWindowPosition();
            viewModel.RefreshWindowsCommand.Execute(null);
        };

        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.IsAdvancedMode) && viewModel.IsAdvancedMode)
                Dispatcher.InvokeAsync(RedrawAdvancedCanvas, System.Windows.Threading.DispatcherPriority.Render);
        };

        if (viewModel.AdvancedVm is not null)
        {
            viewModel.AdvancedVm.Slots.CollectionChanged += (_, _) =>
                Dispatcher.InvokeAsync(RedrawAdvancedCanvas, System.Windows.Threading.DispatcherPriority.Render);
            viewModel.AdvancedVm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(AdvancedViewModel.Slots))
                    Dispatcher.InvokeAsync(RedrawAdvancedCanvas, System.Windows.Threading.DispatcherPriority.Render);
            };
        }

        if (viewModel.CartoVm is not null)
        {
            viewModel.CartoVm.CooldownReady += (_, args) =>
            {
                Dispatcher.Invoke(() =>
                {
                    var (character, cooldown) = args;
                    MessageBox.Show(
                        $"⏱ {character.Name} — {cooldown.Type} est prêt !",
                        "Cooldown prêt",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                });
            };
        }

        Closing += (_, _) => SaveAllSettings();
    }

    private void RestoreWindowPosition()
    {
        var settings = _settingsService.Load();
        Width = settings.WindowWidth;
        Height = settings.WindowHeight;
        if (!double.IsNaN(settings.WindowLeft) && !double.IsNaN(settings.WindowTop))
        {
            Left = settings.WindowLeft;
            Top = settings.WindowTop;
        }
    }

    private void SaveAllSettings()
    {
        var settings = _viewModel.GetCurrentSettings();
        settings.WindowWidth = Width;
        settings.WindowHeight = Height;
        settings.WindowLeft = Left;
        settings.WindowTop = Top;
        _settingsService.Save(settings);
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        else
            DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void Close_Click(object sender, RoutedEventArgs e) =>
        Close();

    private void Help_Click(object sender, RoutedEventArgs e)
    {
        new HelpWindow { Owner = this }.ShowDialog();
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        var ver = version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0";

        System.Windows.Media.Brush Res(string key) =>
            (System.Windows.Media.Brush)Application.Current.Resources[key];
        string Str(string key) =>
            Application.Current.Resources[key] as string ?? key;

        var gold = Res("GoldBrush");
        var brightGold = Res("BrightGoldBrush");
        var text = Res("TextBrush");
        var subtext = Res("SubtextBrush");
        var epic = Res("EpicBrush");
        var bg = Res("WindowBgBrush");
        var accentBorder = Res("AccentBorderBrush");
        var sepBrush = Res("BorderBrush");

        var win = new Window
        {
            Title = Str("About_WindowTitle"),
            Width = 420,
            Height = 380,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent
        };

        var outerBorder = new Border
        {
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(2),
            BorderBrush = accentBorder,
            Background = bg,
            Padding = new Thickness(24, 18, 24, 18)
        };

        var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };

        stack.Children.Add(new TextBlock
        {
            Text = "<Special Azeroth Service>",
            FontSize = 18, FontWeight = FontWeights.Bold,
            Foreground = brightGold, HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 2)
        });
        stack.Children.Add(new TextBlock
        {
            Text = $"Version {ver}",
            FontSize = 11, Foreground = subtext,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 16)
        });

        stack.Children.Add(new Border
        {
            Height = 1, Background = sepBrush,
            Margin = new Thickness(0, 0, 0, 14)
        });

        stack.Children.Add(new TextBlock
        {
            Text = Str("About_Dev"),
            FontSize = 13, Foreground = text, FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 16)
        });

        stack.Children.Add(new TextBlock
        {
            Text = Str("About_Thanks"),
            FontSize = 12, Foreground = gold, FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 10)
        });

        var optiBlock = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 10)
        };
        optiBlock.Inlines.Add(new System.Windows.Documents.Run("Opti") { Foreground = brightGold, FontWeight = FontWeights.Bold });
        optiBlock.Inlines.Add(new System.Windows.Documents.Run(Str("About_Opti")) { Foreground = text, FontSize = 12 });
        stack.Children.Add(optiBlock);

        var eloiBlock = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 340,
            Margin = new Thickness(0, 0, 0, 4)
        };
        eloiBlock.Inlines.Add(new System.Windows.Documents.Run("Eloi") { Foreground = brightGold, FontWeight = FontWeights.Bold });
        eloiBlock.Inlines.Add(new System.Windows.Documents.Run(Str("About_Eloi")) { Foreground = text, FontSize = 12 });
        stack.Children.Add(eloiBlock);
        stack.Children.Add(new TextBlock
        {
            Text = Str("About_Eloi2"),
            FontSize = 12, Foreground = text,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        stack.Children.Add(new TextBlock
        {
            Text = Str("About_Eloi3"),
            FontSize = 11, Foreground = subtext, FontStyle = FontStyles.Italic,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 2, 0, 16)
        });

        stack.Children.Add(new TextBlock
        {
            Text = Str("About_Contact"),
            FontSize = 11, Foreground = subtext,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 8)
        });

        var linkBlock = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 14)
        };
        var hyperlink = new System.Windows.Documents.Hyperlink(
            new System.Windows.Documents.Run(Str("About_Link")))
        {
            NavigateUri = new Uri("https://github.com/yenemillas-Cyco/SpecialAzerothService/releases"),
            Foreground = epic,
            FontSize = 11
        };
        hyperlink.RequestNavigate += (_, nav) =>
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(nav.Uri.AbsoluteUri) { UseShellExecute = true });
            nav.Handled = true;
        };
        linkBlock.Inlines.Add(hyperlink);
        stack.Children.Add(linkBlock);

        stack.Children.Add(new Border
        {
            Height = 1, Background = sepBrush,
            Margin = new Thickness(0, 0, 0, 12)
        });

        var closeBtn = new Button
        {
            Content = Str("About_Close"),
            Padding = new Thickness(24, 8, 24, 8),
            FontSize = 13, FontWeight = FontWeights.Bold, Cursor = Cursors.Hand,
            HorizontalAlignment = HorizontalAlignment.Center,
            Style = (Style)Application.Current.Resources["WowButton"]
        };
        closeBtn.Click += (_, _) => win.Close();
        stack.Children.Add(closeBtn);

        outerBorder.Child = stack;
        outerBorder.MouseLeftButtonDown += (_, _) => win.DragMove();
        win.Content = outerBorder;
        win.ShowDialog();
    }

    // --- Inline rename (double-click label → textbox → Enter/blur → label) ---

    private void DisplayName_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TextBlock label) return;

        var grid = (System.Windows.Controls.Grid)label.Parent;
        var textBox = grid.Children.OfType<TextBox>().First();

        label.Visibility = Visibility.Collapsed;
        textBox.Visibility = Visibility.Visible;
        textBox.Focus();
        textBox.SelectAll();
        e.Handled = true;
    }

    private void RenameBox_LostFocus(object sender, RoutedEventArgs e) => FinishRename((TextBox)sender);

    private void RenameBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Return or Key.Escape)
        {
            FinishRename((TextBox)sender);
            e.Handled = true;
        }
    }

    private void FinishRename(TextBox textBox)
    {
        textBox.Visibility = Visibility.Collapsed;

        var grid = (System.Windows.Controls.Grid)textBox.Parent;
        var label = grid.Children.OfType<TextBlock>().First();
        label.Visibility = Visibility.Visible;

        ((MainViewModel)DataContext).UpdatePreview();
    }

    // --- Advanced list rename ---

    private void AdvName_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TextBlock label) return;
        var grid = (System.Windows.Controls.Grid)label.Parent;
        var textBox = grid.Children.OfType<TextBox>().First();
        label.Visibility = Visibility.Collapsed;
        textBox.Visibility = Visibility.Visible;
        textBox.Focus();
        textBox.SelectAll();
        e.Handled = true;
    }

    private void AdvRenameBox_LostFocus(object sender, RoutedEventArgs e) => FinishAdvRename((TextBox)sender);

    private void AdvRenameBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Return or Key.Escape)
        {
            FinishAdvRename((TextBox)sender);
            e.Handled = true;
        }
    }

    private void FinishAdvRename(TextBox textBox)
    {
        textBox.Visibility = Visibility.Collapsed;
        var grid = (System.Windows.Controls.Grid)textBox.Parent;
        var label = grid.Children.OfType<TextBlock>().First();
        label.Visibility = Visibility.Visible;
        RedrawAdvancedCanvas();
    }

    // --- Preview monitor click ---

    // --- Preview rename ---

    private void PreviewName_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TextBlock label) return;
        if (label.DataContext is not PreviewRect preview || preview.Window is null) return;

        var grid = (System.Windows.Controls.Grid)label.Parent;
        var textBox = grid.Children.OfType<TextBox>().First();

        label.Visibility = Visibility.Collapsed;
        textBox.Visibility = Visibility.Visible;
        textBox.Focus();
        textBox.SelectAll();
        e.Handled = true;
    }

    private void PreviewRenameBox_LostFocus(object sender, RoutedEventArgs e) => FinishPreviewRename((TextBox)sender);

    private void PreviewRenameBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Return or Key.Escape)
        {
            FinishPreviewRename((TextBox)sender);
            e.Handled = true;
        }
    }

    private void FinishPreviewRename(TextBox textBox)
    {
        textBox.Visibility = Visibility.Collapsed;

        var grid = (System.Windows.Controls.Grid)textBox.Parent;
        var label = grid.Children.OfType<TextBlock>().First();
        label.Visibility = Visibility.Visible;

        ((MainViewModel)DataContext).UpdatePreview();
    }

    // --- Drag & Drop reorder ---

    private void WindowList_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
    }

    private void WindowList_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;

        var pos = e.GetPosition(null);
        var diff = _dragStartPoint - pos;

        if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        if (e.OriginalSource is not Visual and not System.Windows.Media.Media3D.Visual3D) return;
        var src = (DependencyObject)e.OriginalSource;
        if (FindAncestor<Button>(src) != null || FindAncestor<CheckBox>(src) != null)
            return;

        var listBox = (ListBox)sender;
        var item = FindAncestor<ListBoxItem>(src);
        if (item?.DataContext is not WindowInfo data) return;

        DragDrop.DoDragDrop(listBox, new DataObject(typeof(WindowInfo), data), DragDropEffects.Move);
        RemoveDropAdorner();
    }

    private void WindowList_DragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(WindowInfo)))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.Effects = DragDropEffects.Move;
        e.Handled = true;

        var pos = e.GetPosition(WindowListBox);
        var (targetItem, insertBefore) = FindDropTarget(pos);

        if (targetItem != null)
            ShowDropAdorner(targetItem, insertBefore);
        else
            RemoveDropAdorner();
    }

    private void WindowList_DragLeave(object sender, DragEventArgs e)
    {
        RemoveDropAdorner();
    }

    private void WindowList_Drop(object sender, DragEventArgs e)
    {
        RemoveDropAdorner();

        if (!e.Data.GetDataPresent(typeof(WindowInfo))) return;

        var droppedData = (WindowInfo)e.Data.GetData(typeof(WindowInfo))!;
        var target = FindItemAtPosition(e.GetPosition(WindowListBox));

        if (target == null || target == droppedData) return;

        var vm = (MainViewModel)DataContext;
        var oldIndex = vm.AvailableWindows.IndexOf(droppedData);
        var newIndex = vm.AvailableWindows.IndexOf(target);

        if (oldIndex < 0 || newIndex < 0 || oldIndex == newIndex) return;

        vm.AvailableWindows.Move(oldIndex, newIndex);
        vm.UpdatePreview();
    }

    // --- Drop indicator adorner ---

    private (ListBoxItem? item, bool insertBefore) FindDropTarget(Point posInListBox)
    {
        for (var i = 0; i < WindowListBox.Items.Count; i++)
        {
            if (WindowListBox.ItemContainerGenerator.ContainerFromIndex(i) is not ListBoxItem container)
                continue;

            var itemPos = container.TranslatePoint(new Point(0, 0), WindowListBox);
            var itemRect = new Rect(itemPos, container.RenderSize);

            if (posInListBox.Y >= itemRect.Top && posInListBox.Y < itemRect.Bottom)
            {
                var midY = itemRect.Top + itemRect.Height / 2;
                return (container, posInListBox.Y < midY);
            }
        }

        // Below all items → after last
        if (WindowListBox.ItemContainerGenerator.ContainerFromIndex(WindowListBox.Items.Count - 1)
            is ListBoxItem last)
            return (last, false);

        return (null, false);
    }

    private void ShowDropAdorner(ListBoxItem targetItem, bool insertBefore)
    {
        var layer = AdornerLayer.GetAdornerLayer(WindowListBox);
        if (layer == null) return;

        var itemPos = targetItem.TranslatePoint(new Point(0, 0), WindowListBox);
        var y = insertBefore ? itemPos.Y : itemPos.Y + targetItem.RenderSize.Height;

        if (_dropAdorner != null && ReferenceEquals(_dropAdorner.AdornedElement, WindowListBox))
        {
            _dropAdorner.UpdateY(y);
            return;
        }

        RemoveDropAdorner();
        _dropAdorner = new DropIndicatorAdorner(WindowListBox, y);
        layer.Add(_dropAdorner);
    }

    private void RemoveDropAdorner()
    {
        if (_dropAdorner == null) return;
        var layer = AdornerLayer.GetAdornerLayer(WindowListBox);
        layer?.Remove(_dropAdorner);
        _dropAdorner = null;
    }

    // --- Helpers ---

    private WindowInfo? FindItemAtPosition(Point pos)
    {
        var element = WindowListBox.InputHitTest(pos) as DependencyObject;
        while (element != null)
        {
            if (element is ListBoxItem lbi)
                return lbi.DataContext as WindowInfo;
            element = VisualTreeHelper.GetParent(element);
        }
        return null;
    }

    private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T match) return match;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}

// --- Advanced Canvas interaction (drag & resize) ---

public partial class MainWindow
{
    private AdvancedSlot? _dragSlot;
    private Point _dragOffset;
    private enum DragMode { None, Move, ResizeTL, ResizeTR, ResizeBL, ResizeBR }
    private DragMode _dragMode = DragMode.None;
    private const double GripSize = 14;
    private const double GripHitZone = 18;
    private const double SnapDistance = 6;

    private void AdvCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(AdvancedCanvas);
        var vm = (MainViewModel)DataContext;
        var advVm = vm.AdvancedVm;
        if (advVm is null) return;

        // Swap mode: clicking a window completes the swap
        if (advVm.IsSwapMode)
        {
            foreach (var slot in advVm.Slots.Reverse())
            {
                var l = slot.CanvasX; var t = slot.CanvasY;
                var r = l + slot.CanvasWidth; var b = t + slot.CanvasHeight;
                if (pos.X >= l && pos.X <= r && pos.Y >= t && pos.Y <= b)
                {
                    advVm.CompleteSwap(slot);
                    RedrawAdvancedCanvas();
                    e.Handled = true;
                    return;
                }
            }
            advVm.IsSwapMode = false;
            e.Handled = true;
            return;
        }

        // Pass 1: check grips of all slots (top to bottom) — grips win everywhere
        foreach (var slot in advVm.Slots.Reverse())
        {
            var l = slot.CanvasX; var t = slot.CanvasY;
            var r = l + slot.CanvasWidth; var b = t + slot.CanvasHeight;

            if (TryGripHit(pos, slot, advVm, l, t, r, b))
            { e.Handled = true; return; }
        }

        // Pass 2: check body of topmost slot for move
        foreach (var slot in advVm.Slots.Reverse())
        {
            var l = slot.CanvasX; var t = slot.CanvasY;
            var r = l + slot.CanvasWidth; var b = t + slot.CanvasHeight;

            if (pos.X >= l && pos.X <= r && pos.Y >= t && pos.Y <= b)
            {
                _dragOffset = new Point(pos.X - l, pos.Y - t);
                StartDrag(slot, advVm, DragMode.Move, pos);
                e.Handled = true;
                return;
            }
        }
    }

    private bool TryGripHit(Point pos, AdvancedSlot slot, AdvancedViewModel advVm,
        double l, double t, double r, double b)
    {
        if (HitCorner(pos, l, t, 1, 1)) { StartDrag(slot, advVm, DragMode.ResizeTL, pos); return true; }
        if (HitCorner(pos, r, t, -1, 1)) { StartDrag(slot, advVm, DragMode.ResizeTR, pos); return true; }
        if (HitCorner(pos, l, b, 1, -1)) { StartDrag(slot, advVm, DragMode.ResizeBL, pos); return true; }
        if (HitCorner(pos, r, b, -1, -1)) { StartDrag(slot, advVm, DragMode.ResizeBR, pos); return true; }
        return false;
    }

    private bool HitCorner(Point pos, double cornerX, double cornerY, double dirX, double dirY)
    {
        var dx = (pos.X - cornerX) * dirX;
        var dy = (pos.Y - cornerY) * dirY;
        return dx >= -4 && dx <= GripHitZone && dy >= -4 && dy <= GripHitZone;
    }

    private void StartDrag(AdvancedSlot slot, AdvancedViewModel advVm, DragMode mode, Point pos)
    {
        _dragSlot = slot;
        _dragMode = mode;
        advVm.SelectedSlot = slot;
        AdvancedCanvas.CaptureMouse();
    }

    private void AdvCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragSlot is null || _dragMode == DragMode.None || e.LeftButton != MouseButtonState.Pressed) return;

        var vm = (MainViewModel)DataContext;
        var advVm = vm.AdvancedVm;
        if (advVm is null) return;

        var pos = e.GetPosition(AdvancedCanvas);
        var s = _dragSlot;

        var l = s.CanvasX;
        var t = s.CanvasY;
        var r = l + s.CanvasWidth;
        var b = t + s.CanvasHeight;

        switch (_dragMode)
        {
            case DragMode.Move:
            {
                var nx = pos.X - _dragOffset.X;
                var ny = pos.Y - _dragOffset.Y;
                (nx, ny) = SnapPos(advVm, s, nx, ny, s.CanvasWidth, s.CanvasHeight);
                s.SetCanvasPos(nx, ny,
                    advVm.GlobalBoundsLeft, advVm.GlobalBoundsTop,
                    advVm.GlobalBoundsRight, advVm.GlobalBoundsBottom);
                break;
            }
            case DragMode.ResizeBR:
            {
                var nr = SnapEdge(advVm, s, pos.X, false);
                var nb = SnapEdge(advVm, s, pos.Y, true);
                s.SetCanvasRect(l, t, nr, nb);
                break;
            }
            case DragMode.ResizeTL:
            {
                var nl = SnapEdge(advVm, s, pos.X, false);
                var nt = SnapEdge(advVm, s, pos.Y, true);
                s.SetCanvasRect(nl, nt, r, b);
                break;
            }
            case DragMode.ResizeTR:
            {
                var nr = SnapEdge(advVm, s, pos.X, false);
                var nt = SnapEdge(advVm, s, pos.Y, true);
                s.SetCanvasRect(l, nt, nr, b);
                break;
            }
            case DragMode.ResizeBL:
            {
                var nl = SnapEdge(advVm, s, pos.X, false);
                var nb = SnapEdge(advVm, s, pos.Y, true);
                s.SetCanvasRect(nl, t, r, nb);
                break;
            }
        }

        RedrawAdvancedCanvas();
    }

    private (double x, double y) SnapPos(AdvancedViewModel advVm, AdvancedSlot self,
        double x, double y, double w, double h)
    {
        var edges = CollectSnapEdges(advVm, self);
        x = TrySnap(x, edges.xEdges) ?? x;
        y = TrySnap(y, edges.yEdges) ?? y;
        var r = x + w; var b = y + h;
        var snapR = TrySnap(r, edges.xEdges);
        if (snapR.HasValue) x = snapR.Value - w;
        var snapB = TrySnap(b, edges.yEdges);
        if (snapB.HasValue) y = snapB.Value - h;
        return (x, y);
    }

    private (List<double> xEdges, List<double> yEdges) CollectSnapEdges(AdvancedViewModel advVm, AdvancedSlot self)
    {
        var xs = new List<double>();
        var ys = new List<double>();

        // Monitor edges
        foreach (var mon in advVm.MonitorOutlines)
        {
            xs.Add(mon.CanvasX); xs.Add(mon.CanvasX + mon.CanvasWidth);
            ys.Add(mon.CanvasY); ys.Add(mon.CanvasY + mon.CanvasHeight);
        }

        // Other slots edges
        foreach (var slot in advVm.Slots)
        {
            if (slot == self) continue;
            xs.Add(slot.CanvasX); xs.Add(slot.CanvasX + slot.CanvasWidth);
            ys.Add(slot.CanvasY); ys.Add(slot.CanvasY + slot.CanvasHeight);
        }

        return (xs, ys);
    }

    private double SnapEdge(AdvancedViewModel advVm, AdvancedSlot self, double val, bool isY)
    {
        var edges = CollectSnapEdges(advVm, self);
        return TrySnap(val, isY ? edges.yEdges : edges.xEdges) ?? val;
    }

    private double? TrySnap(double val, List<double> edges)
    {
        double? best = null;
        var bestDist = SnapDistance;
        foreach (var edge in edges)
        {
            var dist = Math.Abs(val - edge);
            if (dist < bestDist) { bestDist = dist; best = edge; }
        }
        return best;
    }

    private void AdvCanvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragSlot is not null && _dragMode == DragMode.Move)
        {
            var vm = (MainViewModel)DataContext;
            var advVm = vm.AdvancedVm;
            if (advVm is not null)
                advVm.ResolveMonitorAfterDrop(_dragSlot);
        }
        _dragSlot = null;
        _dragMode = DragMode.None;
        AdvancedCanvas.ReleaseMouseCapture();
    }

    internal void RedrawAdvancedCanvas()
    {
        AdvancedCanvas.Children.Clear();
        var vm = (MainViewModel)DataContext;
        var advVm = vm.AdvancedVm;
        if (advVm is null) return;

        // Draw monitor outlines with labels above
        foreach (var mon in advVm.MonitorOutlines)
        {
            var monW = Math.Min(mon.CanvasWidth, AdvancedCanvas.Width - mon.CanvasX);
            var monH = Math.Min(mon.CanvasHeight, AdvancedCanvas.Height - mon.CanvasY);
            if (monW < 5 || monH < 5) continue;

            // Label above the monitor zone — centered
            var monLabel = new TextBlock
            {
                Text = $"🖥 {mon.Label}",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.Resources["GoldBrush"],
                Width = monW,
                TextAlignment = TextAlignment.Center
            };
            Canvas.SetLeft(monLabel, mon.CanvasX);
            Canvas.SetTop(monLabel, Math.Max(0, mon.CanvasY - 18));
            AdvancedCanvas.Children.Add(monLabel);

            // Monitor border (no child label inside)
            var monBorder = new Border
            {
                Width = monW,
                Height = monH,
                BorderBrush = (Brush)Application.Current.Resources["GoldBrush"],
                BorderThickness = new Thickness(2.5),
                Background = new SolidColorBrush(Color.FromArgb(0x0A, 0xFF, 0xFF, 0xFF)),
                CornerRadius = new CornerRadius(4)
            };
            Canvas.SetLeft(monBorder, mon.CanvasX);
            Canvas.SetTop(monBorder, mon.CanvasY);
            AdvancedCanvas.Children.Add(monBorder);
        }

        // Draw window slots
        foreach (var slot in advVm.Slots)
        {
            var isSelected = slot == advVm.SelectedSlot;
            var borderColor = isSelected ? Colors.Orange : Color.FromRgb(0x00, 0x70, 0xDD);
            var bgColor = isSelected
                ? Color.FromArgb(0x44, 0xFF, 0x8C, 0x00)
                : Color.FromArgb(0x33, 0x00, 0x70, 0xDD);

            var drawW = Math.Min(slot.CanvasWidth, AdvancedCanvas.Width - slot.CanvasX);
            var drawH = Math.Min(slot.CanvasHeight, AdvancedCanvas.Height - slot.CanvasY);
            if (drawW < 5 || drawH < 5) continue;

            var border = new Border
            {
                Width = drawW,
                Height = drawH,
                Background = new SolidColorBrush(bgColor),
                BorderBrush = new SolidColorBrush(borderColor),
                BorderThickness = new Thickness(isSelected ? 2 : 1.5),
                CornerRadius = new CornerRadius(3)
            };

            var grid = new Grid { Cursor = Cursors.SizeAll };

            // Badge — number centered at top
            var badge = new Border
            {
                Width = 18, Height = 18,
                CornerRadius = new CornerRadius(9),
                Background = (Brush)Application.Current.Resources["GoldBrush"],
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 2, 0, 0)
            };
            badge.Child = new TextBlock
            {
                Text = slot.Window.LaunchOrder.ToString(),
                FontSize = 10, FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            grid.Children.Add(badge);

            // Star badge for leader — same visual weight as number badge
            if (slot.Window.IsMainWindow)
            {
                var starBadge = new Border
                {
                    Width = 18, Height = 18,
                    CornerRadius = new CornerRadius(9),
                    Background = Brushes.Gold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(42, 2, 0, 0)
                };
                starBadge.Child = new TextBlock
                {
                    Text = "★",
                    FontSize = 13, FontWeight = FontWeights.Bold,
                    Foreground = Brushes.Black,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, -1, 0, 0)
                };
                grid.Children.Add(starBadge);
            }

            // Title — slightly below center
            grid.Children.Add(new TextBlock
            {
                Text = slot.Window.DisplayName,
                FontSize = 10,
                Foreground = (Brush)Application.Current.Resources["TextBrush"],
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 6, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            // 4 corner grips
            var gripBrush = new SolidColorBrush(borderColor) { Opacity = 0.8 };
            foreach (var (ha, va, cursor) in new[]
            {
                (HorizontalAlignment.Left,  VerticalAlignment.Top,    Cursors.SizeNWSE),
                (HorizontalAlignment.Right, VerticalAlignment.Top,    Cursors.SizeNESW),
                (HorizontalAlignment.Left,  VerticalAlignment.Bottom, Cursors.SizeNESW),
                (HorizontalAlignment.Right, VerticalAlignment.Bottom, Cursors.SizeNWSE),
            })
            {
                grid.Children.Add(new Border
                {
                    Width = GripSize, Height = GripSize,
                    Background = gripBrush,
                    HorizontalAlignment = ha,
                    VerticalAlignment = va,
                    CornerRadius = new CornerRadius(3),
                    Cursor = cursor
                });
            }

            border.Child = grid;
            Canvas.SetLeft(border, slot.CanvasX);
            Canvas.SetTop(border, slot.CanvasY);
            AdvancedCanvas.Children.Add(border);
        }
    }
}

/// <summary>Gold line showing where the dragged item will be inserted.</summary>
internal class DropIndicatorAdorner : Adorner
{
    private double _y;
    private static readonly Pen LinePen = new(new SolidColorBrush(Color.FromRgb(0xFF, 0xD1, 0x00)), 2.5);

    public DropIndicatorAdorner(UIElement adornedElement, double y) : base(adornedElement)
    {
        _y = y;
        IsHitTestVisible = false;
    }

    public void UpdateY(double y)
    {
        _y = y;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        var width = AdornedElement.RenderSize.Width;
        dc.DrawLine(LinePen, new Point(4, _y), new Point(width - 4, _y));

        // Small triangles at both ends
        var triangleSize = 5.0;
        var brush = LinePen.Brush;

        var leftTriangle = new StreamGeometry();
        using (var ctx = leftTriangle.Open())
        {
            ctx.BeginFigure(new Point(0, _y), true, true);
            ctx.LineTo(new Point(triangleSize * 2, _y - triangleSize), false, false);
            ctx.LineTo(new Point(triangleSize * 2, _y + triangleSize), false, false);
        }
        dc.DrawGeometry(brush, null, leftTriangle);

        var rightTriangle = new StreamGeometry();
        using (var ctx = rightTriangle.Open())
        {
            ctx.BeginFigure(new Point(width, _y), true, true);
            ctx.LineTo(new Point(width - triangleSize * 2, _y - triangleSize), false, false);
            ctx.LineTo(new Point(width - triangleSize * 2, _y + triangleSize), false, false);
        }
        dc.DrawGeometry(brush, null, rightTriangle);
    }
}
