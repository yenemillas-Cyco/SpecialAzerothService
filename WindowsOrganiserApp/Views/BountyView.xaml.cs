using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WindowsOrganiserApp.Models.Bounty;
using WindowsOrganiserApp.ViewModels;

namespace WindowsOrganiserApp.Views;

public partial class BountyView : UserControl
{
    public BountyView()
    {
        InitializeComponent();
    }

    private BountyViewModel? Vm => DataContext as BountyViewModel;

    private void ToggleRules_Click(object sender, RoutedEventArgs e)
    {
        PopupRules.IsOpen = !PopupRules.IsOpen;
    }

    private void CloseRules_Click(object sender, RoutedEventArgs e)
    {
        PopupRules.IsOpen = false;
        Vm?.SaveRulesCommand.Execute(null);
    }

    private void BountyRow_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && sender is FrameworkElement fe && fe.DataContext is BountyEntry bounty)
        {
            Vm?.EditBountyCommand.Execute(bounty);
            e.Handled = true;
        }
    }
}
