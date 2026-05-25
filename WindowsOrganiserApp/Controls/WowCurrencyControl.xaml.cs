using System.Windows;
using System.Windows.Controls;

namespace WindowsOrganiserApp.Controls;

public partial class WowCurrencyControl : UserControl
{
    public static readonly DependencyProperty CopperTotalProperty =
        DependencyProperty.Register(
            nameof(CopperTotal),
            typeof(long),
            typeof(WowCurrencyControl),
            new PropertyMetadata(0L, OnDisplayChanged));

    public static readonly DependencyProperty IconSizeProperty =
        DependencyProperty.Register(nameof(IconSize), typeof(int), typeof(WowCurrencyControl),
            new PropertyMetadata(16, OnDisplayChanged));

    public static readonly DependencyProperty AmountFontSizeProperty =
        DependencyProperty.Register(nameof(AmountFontSize), typeof(int), typeof(WowCurrencyControl),
            new PropertyMetadata(12, OnDisplayChanged));

    public long CopperTotal
    {
        get => (long)GetValue(CopperTotalProperty);
        set => SetValue(CopperTotalProperty, value);
    }

    public int IconSize
    {
        get => (int)GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    public int AmountFontSize
    {
        get => (int)GetValue(AmountFontSizeProperty);
        set => SetValue(AmountFontSizeProperty, value);
    }

    public WowCurrencyControl() => InitializeComponent();

    private static void OnDisplayChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is WowCurrencyControl ctrl)
            ctrl.Refresh();
    }

    private void Refresh()
    {
        Host.Children.Clear();
        if (CopperTotal <= 0) return;
        Host.Children.Add(WowCurrencyDisplay.Build(CopperTotal, IconSize, AmountFontSize));
    }
}
