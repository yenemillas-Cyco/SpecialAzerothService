using System.Windows;
using System.Windows.Controls;
using WindowsOrganiserApp.ViewModels;

namespace WindowsOrganiserApp.Views;

public partial class ConsoView : UserControl
{
    public ConsoView()
    {
        InitializeComponent();
    }

    private void Minus_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: EditableBossConsumable ebc } && ebc.Quantity > 0)
            ebc.Quantity--;
    }

    private void Plus_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: EditableBossConsumable ebc })
            ebc.Quantity++;
    }

    private void MinusConso_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: UiSelectableConsoItem item } && item.Quantity > 0)
            item.Quantity--;
    }

    private void PlusConso_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: UiSelectableConsoItem item })
            item.Quantity++;
    }
}
