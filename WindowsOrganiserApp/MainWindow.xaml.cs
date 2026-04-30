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
            Title = "À propos",
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
            Text = "Développé par Cyco (ancien stagiaire)",
            FontSize = 13, Foreground = text, FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 16)
        });

        stack.Children.Add(new TextBlock
        {
            Text = "— Remerciements —",
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
        optiBlock.Inlines.Add(new System.Windows.Documents.Run("  —  Bêta-testeur officiel, cobaye volontaire") { Foreground = text, FontSize = 12 });
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
        eloiBlock.Inlines.Add(new System.Windows.Documents.Run("  —  Grand Maître de la guilde,") { Foreground = text, FontSize = 12 });
        stack.Children.Add(eloiBlock);
        stack.Children.Add(new TextBlock
        {
            Text = "dictateur d'Azeroth Service.",
            FontSize = 12, Foreground = text,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        stack.Children.Add(new TextBlock
        {
            Text = "On le remercie parce qu'on n'a pas vraiment le choix.",
            FontSize = 11, Foreground = subtext, FontStyle = FontStyles.Italic,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 2, 0, 16)
        });

        stack.Children.Add(new TextBlock
        {
            Text = "Contact : yenemillas@gmail.com",
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
            new System.Windows.Documents.Run("Téléchargements & mises à jour"))
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
            Content = "Fermer",
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

    // --- Preview monitor click ---

    private void PreviewRect_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement el) return;
        if (el.DataContext is not PreviewRect preview) return;

        var vm = (MainViewModel)DataContext;
        if (preview.IsMonitorOutline && preview.MonitorConfig is not null)
        {
            vm.SelectMonitorConfigCommand.Execute(preview.MonitorConfig);
            e.Handled = true;
        }
    }

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
