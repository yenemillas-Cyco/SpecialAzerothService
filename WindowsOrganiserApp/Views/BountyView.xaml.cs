using System.Windows;
using System.Windows.Controls;
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
}
