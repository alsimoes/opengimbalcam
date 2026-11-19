using System.Collections.Specialized;
using System.Text;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using OpenGimbalCam.Studio.ViewModels;

namespace OpenGimbalCam.Studio;

public partial class MainWindow : Window
{
    private Key? _keyMove;
    private bool _closeReady;
    private WindowNotificationManager? _notifications;

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

        // Tunnel + handledEventsToo: no control may swallow the KeyUp and leave the gimbal spinning.
        AddHandler(KeyUpEvent, OnArrowKeyUp, RoutingStrategies.Tunnel, handledEventsToo: true);

        // Never leave the gimbal moving when the window loses focus.
        Deactivated += (_, _) =>
        {
            _keyMove = null;
            Vm?.StopMove();
        };

        CameraBox.DropDownClosed += (_, _) => Stage.Focus();
        Stage.PointerPressed += (_, _) => Stage.Focus();
    }

    public StudioSettings? Settings { get; init; }

    public StudioSettingsStore? SettingsStore { get; init; }

    private MainViewModel? Vm => DataContext as MainViewModel;

    /// <summary>Applies the saved size and position before the window is shown.</summary>
    public void RestoreWindow(StudioSettings settings)
    {
        if (settings.WindowWidth is { } w && settings.WindowHeight is { } h && w >= MinWidth && h >= MinHeight)
        {
            Width = w;
            Height = h;
        }
        if (settings.WindowX is { } x && settings.WindowY is { } y
            && Screens.All.Any(s => s.WorkingArea.Contains(new PixelPoint(x, y))))
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Position = new PixelPoint(x, y);
        }
        if (settings.WindowMaximized)
            WindowState = WindowState.Maximized;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        Stage.Focus();
        _notifications = new WindowNotificationManager(this) { Position = NotificationPosition.BottomRight, MaxItems = 3 };
        if (Vm is { } vm)
        {
            vm.Notice += OnNotice;
            vm.SnapshotTaken += OnSnapshotTaken;
            vm.Log.CollectionChanged += OnLogChanged;
        }
    }

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (_closeReady)
            return;
        e.Cancel = true;
        IsEnabled = false;
        try
        {
            if (Vm is { } vm)
            {
                if (Settings is not null && SettingsStore is not null)
                {
                    vm.CaptureSettings(Settings);
                    CaptureWindow(Settings);
                    SettingsStore.Save(Settings);
                }
                await vm.DisposeAsync();
            }
        }
        finally
        {
            _closeReady = true;
            Close();
        }
    }

    private void CaptureWindow(StudioSettings s)
    {
        s.WindowMaximized = WindowState == WindowState.Maximized;
        if (WindowState == WindowState.Normal)
        {
            s.WindowWidth = Width;
            s.WindowHeight = Height;
            s.WindowX = Position.X;
            s.WindowY = Position.Y;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.KeyModifiers != KeyModifiers.None || Vm is not { } vm || IsTextEntryFocused())
            return;

        string? direction = e.Key switch
        {
            Key.Up => "up",
            Key.Down => "down",
            Key.Left => "left",
            Key.Right => "right",
            _ => null,
        };
        if (direction is not null)
        {
            if (vm.HasXu)
            {
                // Auto-repeat of the same key must not resend the command.
                if (_keyMove != e.Key)
                {
                    vm.StartMove(direction);
                    _keyMove = e.Key;
                }
                e.Handled = true;
            }
            return;
        }

        switch (e.Key)
        {
            case Key.Home:
                vm.HomeCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.OemPlus or Key.Add:
                vm.ZoomInCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.OemMinus or Key.Subtract:
                vm.ZoomOutCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    private void OnArrowKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key == _keyMove)
        {
            _keyMove = null;
            Vm?.StopMove();
        }
    }

    private bool IsTextEntryFocused() =>
        TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is TextBox or NumericUpDown or Slider or ComboBox or ListBox or ListBoxItem;

    private void OnNotice(object? sender, StudioNotice notice)
    {
        void Show()
        {
            var type = notice.Kind switch
            {
                StatusKind.Success => NotificationType.Success,
                StatusKind.Warning => NotificationType.Warning,
                StatusKind.Error => NotificationType.Error,
                _ => NotificationType.Information,
            };
            Action? onClick = notice.FilePath is { } path ? () => _ = OpenFileAsync(path) : null;
            _notifications?.Show(new Notification(notice.Title, notice.Message, type, TimeSpan.FromSeconds(6), onClick));
        }

        if (Dispatcher.UIThread.CheckAccess())
            Show();
        else
            Dispatcher.UIThread.Post(Show);
    }

    private async Task OpenFileAsync(string path)
    {
        if (File.Exists(path) && TopLevel.GetTopLevel(this)?.Launcher is { } launcher)
            await launcher.LaunchFileInfoAsync(new FileInfo(path));
    }

    private async void OnSnapshotTaken(object? sender, EventArgs e)
    {
        var animation = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(220),
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(OpacityProperty, 0.6) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(OpacityProperty, 0.0) } },
            },
        };
        await animation.RunAsync(FlashOverlay);
    }

    private void OnLogChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add || Vm is not { } vm)
            return;
        Dispatcher.UIThread.Post(() =>
        {
            if (vm.Log.Count > 0)
                ActivityList.ScrollIntoView(vm.Log.Count - 1);
        }, DispatcherPriority.Background);
    }

    private async void OnCopyLog(object? sender, RoutedEventArgs e)
    {
        if (Vm is not { } vm || Clipboard is not { } clipboard)
            return;
        var text = new StringBuilder();
        foreach (var entry in vm.Log)
            text.Append(entry.Time).Append(' ').AppendLine(entry.Text);
        await clipboard.SetTextAsync(text.ToString());
    }

    private async void OnOpenPhotos(object? sender, RoutedEventArgs e) => await OpenFolderAsync(Vm?.PhotosFolder);

    private async void OnOpenVideos(object? sender, RoutedEventArgs e) => await OpenFolderAsync(Vm?.VideosFolder);

    private async Task OpenFolderAsync(string? path)
    {
        if (path is null || Launcher is not { } launcher)
            return;
        Directory.CreateDirectory(path);
        await launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(path));
    }

    private void OnStageDoubleTapped(object? sender, TappedEventArgs e) => Vm?.ToggleFocusModeCommand.Execute(null);

    private async void OnAbout(object? sender, RoutedEventArgs e) => await new AboutWindow().ShowDialog(this);
}
