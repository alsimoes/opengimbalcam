using Avalonia.Controls;
using Avalonia.Interactivity;
using OpenGimbalCam.Studio.ViewModels;

namespace OpenGimbalCam.Studio;

public partial class AboutWindow : Window
{
    private readonly AboutViewModel _viewModel = new();

    public AboutWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
    }

    private async void OnRepository(object? sender, RoutedEventArgs e)
    {
        if (Launcher is { } launcher)
            await launcher.LaunchUriAsync(new Uri(_viewModel.RepositoryUrl));
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
