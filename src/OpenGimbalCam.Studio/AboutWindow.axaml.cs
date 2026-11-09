using Avalonia.Controls;
using Avalonia.Interactivity;
using OpenGimbalCam.Studio.ViewModels;

namespace OpenGimbalCam.Studio;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        DataContext = new AboutViewModel();
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
