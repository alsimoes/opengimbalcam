using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using OpenGimbalCam.Devices;
using OpenGimbalCam.Studio.ViewModels;

namespace OpenGimbalCam.Studio;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var vm = new MainViewModel(CameraPlatform.Create());
            desktop.MainWindow = new MainWindow { DataContext = vm };
            desktop.ShutdownRequested += (_, _) => vm.Dispose();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
