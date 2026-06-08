using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using SpecialAzerothService.Core.Models.WowSync;
using SpecialAzerothService.Core.Services;

namespace WindowsOrganiserApp.Controls;

public partial class WowItemSlot : UserControl
{
    public static IWowItemLookupService? LookupService { get; set; }

    private CancellationTokenSource? _loadCts;

    public WowItemSlot()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Unloaded += (_, _) => CancelLoad();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        CancelLoad();

        if (e.NewValue is not WowItem item)
        {
            IconImage.Source = null;
            FallbackText.Visibility = Visibility.Collapsed;
            ToolTip = null;
            return;
        }

        ToolTip = WowItemTooltipBuilder.Create(item, null);
        _ = LoadAsync(item);
    }

    private void CancelLoad()
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = null;
    }

    private async Task LoadAsync(WowItem item)
    {
        var cts = new CancellationTokenSource();
        _loadCts = cts;

        ImageSource? image = null;
        WowheadItemDetails? details = null;

        if (LookupService != null && (item.ItemId > 0 || item.SpellId > 0))
        {
            try
            {
                var iconTask = LookupService.GetIconAsync(item, cts.Token);
                Task<WowheadItemDetails?> detailsTask = item.ItemId > 0
                    ? LookupService.GetDetailsAsync(item, cts.Token)
                    : LookupService.GetSpellDetailsAsync(item.SpellId, cts.Token);
                await Task.WhenAll(iconTask, detailsTask).ConfigureAwait(false);
                if (!cts.IsCancellationRequested)
                {
                    image = await iconTask.ConfigureAwait(false);
                    details = await detailsTask.ConfigureAwait(false);
                }
            }
            catch
            {
                // Wowhead indisponible : icône / tooltip de secours
            }
        }

        if (cts.IsCancellationRequested) return;

        await Dispatcher.InvokeAsync(() =>
        {
            if (cts.IsCancellationRequested) return;
            IconImage.Source = image;
            FallbackText.Visibility = image == null ? Visibility.Visible : Visibility.Collapsed;
            ToolTip = WowItemTooltipBuilder.Create(item, details);
        });
    }
}

public class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is int count && count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
