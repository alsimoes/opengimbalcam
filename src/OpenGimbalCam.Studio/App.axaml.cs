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
            var store = StudioSettingsStore.Default;
            var settings = store.Load();
            var vm = new MainViewModel(CameraPlatform.Create(), settings);
            var window = new MainWindow { DataContext = vm, Settings = settings, SettingsStore = store };
            window.RestoreWindow(settings);
            desktop.MainWindow = window;
        }
        base.OnFrameworkInitializationCompleted();
    }
}
