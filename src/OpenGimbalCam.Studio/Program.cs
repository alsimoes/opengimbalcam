using Avalonia;

namespace OpenGimbalCam.Studio;

internal static class Program
{
    // Environment.Exit: third-party foreground threads (DirectShow/FlashCap) must not keep the
    // process alive after the orderly shutdown (the window already disposed everything).
    [STAThread]
    public static void Main(string[] args) =>
        Environment.Exit(BuildAvaloniaApp().StartWithClassicDesktopLifetime(args));

    // Also used by the Avalonia previewer.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace();
}
