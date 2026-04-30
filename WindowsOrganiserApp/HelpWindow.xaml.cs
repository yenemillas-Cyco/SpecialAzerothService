using System.Windows;
using System.Windows.Input;

namespace WindowsOrganiserApp;

public partial class HelpWindow : Window
{
    public HelpWindow()
    {
        InitializeComponent();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) =>
        DragMove();

    private void Close_Click(object sender, RoutedEventArgs e) =>
        Close();
}
