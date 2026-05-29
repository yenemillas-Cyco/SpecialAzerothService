using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using WindowsOrganiserApp.ViewModels;

namespace WindowsOrganiserApp.Views;

public partial class CraftCraftingView
{
    public CraftCraftingView()
    {
        InitializeComponent();
        PreviewMouseDown += OnPreviewMouseDownCloseStockAccountsDropDown;
    }

    private void OnPreviewMouseDownCloseStockAccountsDropDown(object sender, MouseButtonEventArgs e)
    {
        if (StockAccountsDropDown.IsChecked != true)
            return;

        var source = e.OriginalSource as DependencyObject;
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

    private CraftCraftingViewModel? Vm => DataContext as CraftCraftingViewModel;

    private void ListName_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is not CraftListSummary summary)
            return;
        if (ListsListBox.SelectedItem != summary)
            return;

        Vm?.BeginRenameList(summary);
        e.Handled = true;
    }

    private void RenameTextBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox box) return;
        box.Focus();
        box.SelectAll();
    }

    private void RenameTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (Vm == null) return;

        if (e.Key == Key.Enter)
        {
            Vm.CommitRenameList();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Vm.CancelRenameList();
            e.Handled = true;
        }
    }

    private void RenameTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (Vm?.RenamingList == null) return;
        if (sender is TextBox box && box.DataContext is CraftListSummary summary && summary.IsRenaming)
            Vm.CommitRenameList();
    }

    private void CraftQtyDisplay_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is not CraftListRow row)
            return;

        row.BeginEditQuantity();
        e.Handled = true;
    }

    private void CraftQtyTextBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox box) return;
        box.Focus();
        box.SelectAll();
    }

    private void CraftQtyTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox box || box.DataContext is not CraftListRow row)
            return;

        if (e.Key == Key.Enter)
        {
            row.CommitEditQuantity();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            row.CancelEditQuantity();
            e.Handled = true;
        }
    }

    private void CraftQtyTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox box && box.DataContext is CraftListRow row && row.IsEditingQuantity)
            row.CommitEditQuantity();
    }
}
