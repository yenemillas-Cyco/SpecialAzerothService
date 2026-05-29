using System.Windows;
using System.Windows.Input;
using WindowsOrganiserApp.ViewModels;

namespace WindowsOrganiserApp.Views;

public partial class CraftPickerWindow : Window
{
    public CraftPickerWindow(CraftViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
