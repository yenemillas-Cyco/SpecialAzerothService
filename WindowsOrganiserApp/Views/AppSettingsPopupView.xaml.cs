using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using WindowsOrganiserApp.ViewModels;

namespace WindowsOrganiserApp.Views;

public partial class AppSettingsPopupView : UserControl
{
    private CartoViewModel? Vm => DataContext as CartoViewModel;
    private bool _isDragging;
    private Point _dragStart;
    private double _dragBaseX;
    private double _dragBaseY;
    private PropertyChangedEventHandler? _vmPropertyChangedHandler;
    private CartoViewModel? _subscribedVm;

    public event EventHandler? SettingsSaved;

    public AppSettingsPopupView()
    {
        InitializeComponent();
        Loaded += (_, _) => PopupSettings.PlacementTarget = Window.GetWindow(this);
        DataContextChanged += (_, _) => WireViewModel();
    }

    public void Open()
    {
        Dispatcher.BeginInvoke(ShowPopup, DispatcherPriority.Input);
    }

    private void WireViewModel()
    {
        if (_subscribedVm != null && _vmPropertyChangedHandler != null)
            _subscribedVm.PropertyChanged -= _vmPropertyChangedHandler;

        _subscribedVm = Vm;
        if (_subscribedVm == null)
        {
            _vmPropertyChangedHandler = null;
            return;
        }

        _vmPropertyChangedHandler = (_, e) =>
        {
            if (e.PropertyName == nameof(CartoViewModel.IsSettingsPanelOpen))
                SyncFromViewModel();
        };
        _subscribedVm.PropertyChanged += _vmPropertyChangedHandler;
    }

    private void SyncFromViewModel()
    {
        if (Vm == null)
            return;

        if (!Vm.IsSettingsPanelOpen && PopupSettings.IsOpen)
            PopupSettings.IsOpen = false;
    }

    private void ShowPopup()
    {
        if (Vm == null)
            return;

        PopupSettings.PlacementTarget = Window.GetWindow(this);
        PopupSettings.IsOpen = true;

        if (!Vm.IsSettingsPanelOpen)
            Vm.IsSettingsPanelOpen = true;

        _ = Dispatcher.InvokeAsync(PositionCentered, DispatcherPriority.Loaded);
    }

    private void PositionCentered()
    {
        var anchor = PopupSettings.PlacementTarget as FrameworkElement;
        if (anchor == null)
            return;

        var areaW = anchor.ActualWidth > 1 ? anchor.ActualWidth : 0;
        var areaH = anchor.ActualHeight > 1 ? anchor.ActualHeight : 0;
        if (areaW < 1 && anchor is Window win)
        {
            areaW = win.ActualWidth;
            areaH = win.ActualHeight;
        }

        if (areaW < 1 || areaH < 1)
            return;

        const double defaultW = 460;
        const double defaultH = 420;
        var w = SettingsPopupBorder.ActualWidth > 1 ? SettingsPopupBorder.ActualWidth : defaultW;
        var h = SettingsPopupBorder.ActualHeight > 1 ? SettingsPopupBorder.ActualHeight : defaultH;

        PopupSettings.HorizontalOffset = Math.Max(8, (areaW - w) / 2);
        PopupSettings.VerticalOffset = Math.Max(8, (areaH - h) / 2);
    }

    private FrameworkElement DragAnchor =>
        PopupSettings.PlacementTarget as FrameworkElement ?? this;

    private void SettingsPopupDragBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
            return;

        _isDragging = true;
        _dragStart = e.GetPosition(DragAnchor);
        _dragBaseX = PopupSettings.HorizontalOffset;
        _dragBaseY = PopupSettings.VerticalOffset;
        SettingsPopupDragBar.CaptureMouse();
        e.Handled = true;
    }

    private void SettingsPopupDragBar_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging)
            return;

        var pos = e.GetPosition(DragAnchor);
        var dx = pos.X - _dragStart.X;
        var dy = pos.Y - _dragStart.Y;
        PopupSettings.HorizontalOffset = Math.Max(0, _dragBaseX + dx);
        PopupSettings.VerticalOffset = Math.Max(0, _dragBaseY + dy);
    }

    private void SettingsPopupDragBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging)
            return;

        _isDragging = false;
        SettingsPopupDragBar.ReleaseMouseCapture();
        e.Handled = true;
    }

    private void PopupSettings_Closed(object? sender, EventArgs e)
    {
        if (Vm?.IsSettingsPanelOpen == true)
            Vm.IsSettingsPanelOpen = false;
    }

    private void CloseSettings_Click(object sender, RoutedEventArgs e)
    {
        if (Vm == null)
            return;

        e.Handled = true;
        Vm.IsSettingsPanelOpen = false;
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        if (Vm == null)
            return;

        Vm.CloseSettingsPanelAfterSave();
        SettingsSaved?.Invoke(this, EventArgs.Empty);
    }

    private async void RefreshSettings_Click(object sender, RoutedEventArgs e)
    {
        if (Vm == null)
            return;

        await Vm.RescanWowFromWtfAsync();
    }

    private void AccountUserCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Vm == null || sender is not ComboBox combo || combo.DataContext is not AccountSettingRow row)
            return;

        row.RefreshOwnerDisplayName(Vm.GetOrderedUsers());
    }
}
