using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace WindowsOrganiserApp.Views;

public partial class ReputationView
{
    public ReputationView()
    {
        InitializeComponent();
        PreviewMouseDown += OnPreviewMouseDownCloseStockAccountsDropDown;
    }

    private void OnPreviewMouseDownCloseStockAccountsDropDown(object sender, MouseButtonEventArgs e)
    {
        if (StockAccountsDropDown.IsChecked != true)
            return;

        if (e.OriginalSource is not DependencyObject source)
            return;

        if (IsDescendantOf(StockAccountsDropDown, source)
            || IsDescendantOf(StockAccountsPopup.Child, source))
            return;

        StockAccountsDropDown.IsChecked = false;
    }

    private static bool IsDescendantOf(DependencyObject? ancestor, DependencyObject? node)
    {
        while (node != null)
        {
            if (node == ancestor) return true;
            node = VisualTreeHelper.GetParent(node);
        }

        return false;
    }
}
