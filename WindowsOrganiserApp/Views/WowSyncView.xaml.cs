using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WindowsOrganiserApp.Models.WowSync;
using WindowsOrganiserApp.ViewModels;

namespace WindowsOrganiserApp.Views;

public partial class WowSyncView : UserControl
{
    public WowSyncView()
    {
        InitializeComponent();
    }

    private void Character_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: WowCharacterData character } &&
            DataContext is WowSyncViewModel vm)
        {
            vm.SelectedCharacter = character;
        }
    }
}
