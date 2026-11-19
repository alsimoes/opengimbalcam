using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using OpenGimbalCam.Core.Camera;
using OpenGimbalCam.Studio.ViewModels;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(OpenGimbalCam.Studio.Tests.TestAppBuilder))]

namespace OpenGimbalCam.Studio.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .WithInterFont();
}

public class ControlCatalogTests
{
    private static readonly HashSet<CameraControl> Gimbal =
        [CameraControl.Pan, CameraControl.Tilt, CameraControl.Zoom, CameraControl.PanRelative, CameraControl.TiltRelative];

    [Fact]
    public void Every_non_gimbal_control_has_a_portuguese_label_and_icon()
    {
        foreach (var control in Enum.GetValues<CameraControl>().Where(c => !Gimbal.Contains(c)))
        {
            var spec = ControlCatalog.Get(control);
            Assert.NotEqual(control.ToString(), spec.Label);
            Assert.NotEqual("IconSliders", spec.IconKey);
        }
    }

    [Theory]
    [InlineData(-7, -13, "1/128 s")]
    [InlineData(-2, -13, "1/4 s")]
    [InlineData(333, 1, "1/30 s")]
    [InlineData(20000, 1, "2 s")]
    public void Shutter_is_formatted_as_exposure_time(int value, int rangeMin, string expected) =>
        Assert.Equal(expected, ControlCatalog.FormatShutter(value, new ControlRange(rangeMin, 100000, 1, 0, ControlFlags.Manual)));

    [Fact]
    public void White_balance_shows_kelvin() =>
        Assert.Equal("5200 K", ControlCatalog.FormatValue(CameraControl.WhiteBalance, 5200, new ControlRange(2800, 6500, 100, 4600, ControlFlags.Auto)));
}

public class ControlViewModelTests
{
    private static ControlViewModel Make(FakeSession session, CameraControl control) =>
        new(session, control, session.GetRange(control)!.Value, ex => throw ex);

    [Fact]
    public void Reset_restores_default_and_automatic_where_available()
    {
        var session = new FakeSession(FakeCameraPlatform.Tiny);
        var wb = Make(session, CameraControl.WhiteBalance);
        Assert.False(wb.Auto);
        Assert.True(wb.IsModified);
        wb.ResetCommand.Execute(null);
        Assert.True(wb.Auto);
        Assert.False(wb.IsModified);
        Assert.Equal("Auto", wb.DisplayValue);
        Assert.Equal((CameraControl.WhiteBalance, 4600, ControlFlags.Auto), session.Writes[^1]);

        var brightness = Make(session, CameraControl.Brightness);
        Assert.True(brightness.IsModified);
        brightness.ResetCommand.Execute(null);
        Assert.False(brightness.IsModified);
        Assert.Equal(128, (int)brightness.Value);
    }

    [Fact]
    public void Anti_flicker_uses_options_and_backlight_is_a_toggle()
    {
        var session = new FakeSession(FakeCameraPlatform.Tiny);
        var flicker = Make(session, CameraControl.PowerlineFrequency);
        Assert.True(flicker.IsOptions);
        Assert.Equal(["Desligado", "50 Hz", "60 Hz"], flicker.Options);
        Assert.Equal("50 Hz", flicker.DisplayValue);
        flicker.SelectedOption = 2;
        Assert.Equal(2, (int)flicker.Value);
        flicker.SelectedOption = -1;
        Assert.Equal(2, (int)flicker.Value);

        var backlight = Make(session, CameraControl.BacklightCompensation);
        Assert.True(backlight.IsToggle);
        Assert.False(backlight.IsSlider);
        Assert.Equal("Desligado", backlight.DisplayValue);
        backlight.ToggleValue = true;
        Assert.Equal(1, (int)backlight.Value);
        Assert.Equal("Ligado", backlight.DisplayValue);
    }

    [Fact]
    public void Shutter_slider_shows_the_exposure_time()
    {
        var session = new FakeSession(FakeCameraPlatform.Tiny);
        var shutter = Make(session, CameraControl.Exposure);
        Assert.True(shutter.IsSlider);
        shutter.Auto = false;
        shutter.Value = -7;
        Assert.Equal("1/128 s", shutter.DisplayValue);
    }
}

public class StudioSettingsStoreTests
{
    [Fact]
    public void Settings_round_trip_and_corrupted_file_falls_back_to_defaults()
    {
        string dir = Path.Combine(Path.GetTempPath(), "ogc-settings-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new StudioSettingsStore(Path.Combine(dir, "settings.json"));
            Assert.True(store.Load().PreviewEnabled);

            store.Save(new StudioSettings { Speed = 70, OscPort = 9001, OscTcp = true, WindowWidth = 1400, InspectorTab = 2 });
            var loaded = store.Load();
            Assert.Equal(70, loaded.Speed);
            Assert.Equal(9001, loaded.OscPort);
            Assert.True(loaded.OscTcp);
            Assert.Equal(1400, loaded.WindowWidth);
            Assert.Equal(2, loaded.InspectorTab);

            File.WriteAllText(store.FilePath, "{ not json");
            Assert.Equal(50, store.Load().Speed);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }
}

public class MainViewModelTests
{
    private static StudioSettings NoPreview() => new() { PreviewEnabled = false };

    [Fact]
    public void FormatElapsed_is_hh_mm_ss() =>
        Assert.Equal("01:02:03", MainViewModel.FormatElapsed(new TimeSpan(1, 2, 3)));

    [AvaloniaFact]
    public void Without_a_camera_the_stage_explains_what_to_do()
    {
        var vm = new MainViewModel(new FakeCameraPlatform(false), NoPreview());
        Assert.False(vm.HasCamera);
        Assert.Equal("Nenhuma câmera conectada", vm.StageTitle);
        Assert.Equal("Sem câmera", vm.ConnectionText);
        Assert.False(vm.CanTurnOnPreview);
    }

    [AvaloniaFact]
    public void Restore_image_defaults_resets_every_control()
    {
        var platform = new FakeCameraPlatform(true);
        var vm = new MainViewModel(platform, NoPreview());
        Assert.True(vm.HasCamera);
        Assert.Equal("Vídeo desligado", vm.StageTitle);
        Assert.True(vm.CanTurnOnPreview);
        Assert.Equal(["EXPOSIÇÃO", "COR", "TOM E DETALHE"], vm.ControlGroups.Select(g => g.Title));
        Assert.Contains(vm.Controls, c => c.IsModified);

        vm.RestoreImageDefaultsCommand.Execute(null);

        Assert.All(vm.Controls, c => Assert.False(c.IsModified));
        Assert.Equal(StatusKind.Success, vm.StatusKind);
        Assert.Equal(UiText.ImageRestored, vm.Status);
    }

    [AvaloniaFact]
    public void Zoom_is_clamped_between_0_and_100()
    {
        var vm = new MainViewModel(new FakeCameraPlatform(true), NoPreview());
        vm.Zoom = 95;
        vm.ZoomInCommand.Execute(null);
        Assert.Equal(100, vm.Zoom);
        vm.Zoom = 5;
        vm.ZoomOutCommand.Execute(null);
        Assert.Equal(0, vm.Zoom);
    }

    [AvaloniaFact]
    public void Settings_are_captured_and_applied()
    {
        var settings = new StudioSettings
        {
            PreviewEnabled = false, Speed = 80, OscPort = 9100, OscUdp = false, OscTcp = true,
            OscObsbotCompatibility = false, InspectorTab = 1, ShowGrid = true, CameraPath = FakeCameraPlatform.Tiny.Path,
        };
        var vm = new MainViewModel(new FakeCameraPlatform(true), settings);
        Assert.Equal(80, vm.Speed);
        Assert.Equal(9100, vm.OscPort);
        Assert.True(vm.OscTcp);
        Assert.False(vm.OscUdp);
        Assert.Equal(1, vm.InspectorTabIndex);
        Assert.True(vm.ShowGrid);
        Assert.Equal(FakeCameraPlatform.Tiny.Path, vm.SelectedCamera?.Path);

        vm.Speed = 20;
        vm.ShowGrid = false;
        var captured = new StudioSettings();
        vm.CaptureSettings(captured);
        Assert.Equal(20, captured.Speed);
        Assert.False(captured.ShowGrid);
        Assert.Equal(9100, captured.OscPort);
        Assert.Equal(FakeCameraPlatform.Tiny.Path, captured.CameraPath);
    }

    [AvaloniaFact]
    public async Task Dispose_is_asynchronous_and_idempotent()
    {
        var vm = new MainViewModel(new FakeCameraPlatform(true), NoPreview());
        await vm.DisposeAsync();
        await vm.DisposeAsync();
        Assert.False(vm.HasCamera);
    }
}

public class SnapshotTests
{
    private static readonly string Dir = Path.Combine(AppContext.BaseDirectory, "ui-snapshots");

    private static MainViewModel Live(bool recording = true)
    {
        var vm = new MainViewModel(new FakeCameraPlatform(true), new StudioSettings { PreviewEnabled = false });
        vm.Frame = FakeFrames.Gradient();
        vm.PreviewInfo = "JPEG 1280x720 @ 30 fps";
        vm.PreviewState = PreviewState.Live;
        vm.IsRecording = recording;
        vm.PanText = "12°";
        vm.TiltText = "-5°";
        return vm;
    }

    private static void Shoot(Window window, string name, int width, int height)
    {
        Directory.CreateDirectory(Dir);
        window.Width = width;
        window.Height = height;
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        Assert.Equal(width, frame!.PixelSize.Width);
        Assert.Equal(height, frame.PixelSize.Height);
        frame.Save(Path.Combine(Dir, name));
        window.Close();
    }

    [AvaloniaFact]
    public void Main_empty() =>
        Shoot(new MainWindow { DataContext = new MainViewModel(new FakeCameraPlatform(false), new StudioSettings { PreviewEnabled = false }) },
            "main-empty.png", 1280, 800);

    [AvaloniaFact]
    public void Main_live() => Shoot(new MainWindow { DataContext = Live() }, "main-live.png", 1280, 800);

    [AvaloniaFact]
    public void Main_min_size() => Shoot(new MainWindow { DataContext = Live() }, "main-min.png", 1100, 700);

    [AvaloniaFact]
    public void Main_focus_mode()
    {
        var vm = Live();
        vm.IsFocusMode = true;
        Shoot(new MainWindow { DataContext = vm }, "main-focus.png", 1280, 800);
    }

    [AvaloniaFact]
    public void Main_osc_tab()
    {
        var vm = Live(recording: false);
        vm.InspectorTabIndex = 1;
        Shoot(new MainWindow { DataContext = vm }, "main-osc.png", 1280, 800);
    }

    [AvaloniaFact]
    public void Main_activity_tab()
    {
        var vm = Live(recording: false);
        vm.InspectorTabIndex = 2;
        vm.Log.Add(new LogEntry("10:21:03.114", "vídeo: OBSBOT Tiny (simulada), JPEG 1280x720 @ 30 fps", false));
        vm.Log.Add(new LogEntry("10:21:07.402", "escutando OSC/UDP em 0.0.0.0:16284", false));
        vm.Log.Add(new LogEntry("10:21:12.870", "erro: porta em uso", true));
        Shoot(new MainWindow { DataContext = vm }, "main-activity.png", 1280, 800);
    }

    [AvaloniaFact]
    public void About()
    {
        var window = new AboutWindow();
        Directory.CreateDirectory(Dir);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        Assert.Equal(520, frame!.PixelSize.Width);
        frame.Save(Path.Combine(Dir, "about.png"));
        window.Close();
    }
}
