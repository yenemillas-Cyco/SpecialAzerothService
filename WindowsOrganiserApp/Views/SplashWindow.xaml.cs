using System.Windows;
using WindowsOrganiserApp.Services;

namespace WindowsOrganiserApp.Views;

public partial class SplashWindow
{
    public SplashWindow()
    {
        InitializeComponent();
    }

    public void Report(StartupLoadProgress progress)
    {
        LoadProgressBar.Value = Math.Clamp(progress.Percent, 0, 100);
        StatusText.Text = progress.Message;
    }
}
