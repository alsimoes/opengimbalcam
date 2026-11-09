using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using OpenGimbalCam.Studio.ViewModels;

namespace OpenGimbalCam.Studio;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Hold to move, like TouchOSC's OBSBOT layout: speed on press, 0 on release.
        // Button marks pointer events handled, hence handledEventsToo.
        foreach (var (button, direction) in new[] { (UpButton, "up"), (DownButton, "down"), (LeftButton, "left"), (RightButton, "right") })
        {
            button.AddHandler(PointerPressedEvent, (_, _) => Vm?.StartMove(direction), RoutingStrategies.Bubble, handledEventsToo: true);
            button.AddHandler(PointerReleasedEvent, (_, _) => Vm?.StopMove(), RoutingStrategies.Bubble, handledEventsToo: true);
            button.AddHandler(PointerCaptureLostEvent, (_, _) => Vm?.StopMove(), RoutingStrategies.Bubble, handledEventsToo: true);
        }

        // Never leave the gimbal moving when the window loses focus.
        Deactivated += (_, _) => Vm?.StopMove();
    }

    private async void OnAbout(object? sender, RoutedEventArgs e) => await new AboutWindow().ShowDialog(this);

    private MainViewModel? Vm => DataContext as MainViewModel;
}
