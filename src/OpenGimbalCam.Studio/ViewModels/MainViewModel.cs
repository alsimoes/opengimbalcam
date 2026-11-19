using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenGimbalCam.Core.Camera;
using OpenGimbalCam.Osc;
using OpenGimbalCam.Osc.Bridge;

namespace OpenGimbalCam.Studio.ViewModels;

public sealed partial class MainViewModel : ObservableObject, IPcMedia, IAsyncDisposable
{
    private const int MaxLogLines = 500;
    private const int ErrorNoticeIntervalMs = 3000;

    // Gimbal, zoom and FOV have their own widgets; the relative ranges only scale speeds.
    private static readonly HashSet<CameraControl> GimbalControls =
        [CameraControl.Pan, CameraControl.Tilt, CameraControl.Zoom, CameraControl.PanRelative, CameraControl.TiltRelative];

    private readonly ICameraPlatform _platform;
    private readonly string? _preferredCameraPath;
    private ObsbotCamera? _camera;
    private bool _loading;
    private bool _moving;
    private ObsbotCameraHub? _oscHub;
    private OscBridge? _oscBridge;
    private readonly List<OscServer> _oscServers = [];
    private CameraPreview? _preview;
    private int _previewGeneration;
    private Bitmap? _previousFrame;
    private DispatcherTimer? _recordTimer;
    private DispatcherTimer? _positionTimer;
    private readonly Stopwatch _recordWatch = new();
    private int _oscRunningPort;
    private string _oscRunningTransports = "";
    private string? _lastErrorText;
    private long _lastErrorTick;

    [ObservableProperty]
    private Bitmap? _frame;

    [ObservableProperty]
    private bool _previewEnabled = true;

    [ObservableProperty]
    private string _previewInfo = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RecordButtonText))]
    private bool _isRecording;

    [ObservableProperty]
    private string _recordingElapsed = FormatElapsed(TimeSpan.Zero);

    [ObservableProperty]
    private CameraInfo? _selectedCamera;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStatusError))]
    [NotifyPropertyChangedFor(nameof(IsStatusSuccess))]
    [NotifyPropertyChangedFor(nameof(IsStatusInfo))]
    private StatusKind _statusKind;

    [ObservableProperty]
    private string _status = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConnectedFull))]
    [NotifyPropertyChangedFor(nameof(IsConnectedUvcOnly))]
    [NotifyPropertyChangedFor(nameof(ConnectionText))]
    private bool _hasCamera;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConnectedFull))]
    [NotifyPropertyChangedFor(nameof(IsConnectedUvcOnly))]
    [NotifyPropertyChangedFor(nameof(ConnectionText))]
    private bool _hasXu;

    [ObservableProperty]
    private PreviewState _previewState;

    [ObservableProperty]
    private string _previewError = "";

    [ObservableProperty]
    private string _panText = UiText.NoPosition;

    [ObservableProperty]
    private string _tiltText = UiText.NoPosition;

    [ObservableProperty]
    private double _speed = 50;

    [ObservableProperty]
    private double _zoom;

    [ObservableProperty]
    private int _fovIndex = -1;

    [ObservableProperty]
    private bool _mirror;

    [ObservableProperty]
    private int _inspectorTabIndex;

    [ObservableProperty]
    private bool _isFocusMode;

    [ObservableProperty]
    private bool _showGrid;

    [ObservableProperty]
    private decimal? _oscPort = OscServer.DefaultPort;

    [ObservableProperty]
    private bool _oscUdp = true;

    [ObservableProperty]
    private bool _oscTcp;

    /// <summary>OBSBOT Center OSC protocol compatibility (ADR-0009): on by default, can change live.</summary>
    [ObservableProperty]
    private bool _oscObsbotCompatibility = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OscButtonText))]
    [NotifyPropertyChangedFor(nameof(OscStatusText))]
    [NotifyPropertyChangedFor(nameof(OscStateText))]
    private bool _oscRunning;

    public MainViewModel(ICameraPlatform platform, StudioSettings? settings = null)
    {
        _platform = platform;
        if (settings is not null)
        {
            PreviewEnabled = settings.PreviewEnabled;
            Speed = Math.Clamp(settings.Speed, 1, 100);
            OscPort = settings.OscPort;
            OscUdp = settings.OscUdp;
            OscTcp = settings.OscTcp;
            OscObsbotCompatibility = settings.OscObsbotCompatibility;
            InspectorTabIndex = Math.Clamp(settings.InspectorTab, 0, 2);
            ShowGrid = settings.ShowGrid;
            _preferredCameraPath = settings.CameraPath;
        }
        Refresh();
    }

    public ObservableCollection<CameraInfo> Cameras { get; } = [];

    public ObservableCollection<ControlViewModel> Controls { get; } = [];

    public ObservableCollection<ControlGroupViewModel> ControlGroups { get; } = [];

    public ObservableCollection<LogEntry> Log { get; } = [];

    /// <summary>OSC SetView / libdev cameraSetFovU values 0, 1, 2.</summary>
    public IReadOnlyList<string> FovOptions { get; } = ["86°", "78°", "65°"];

    public string PlatformName => _platform.Name;

    public string AppVersionText => "v" + AppInfo.Version;

    public string PhotosFolder => MediaFolder(Environment.SpecialFolder.MyPictures);

    public string VideosFolder => MediaFolder(Environment.SpecialFolder.MyVideos);

    public string OscButtonText => OscRunning ? UiText.OscStop : UiText.OscStart;

    public string OscStatusText => OscRunning ? UiText.OscStatus(_oscRunningPort) : UiText.OscOff;

    public string OscStateText => OscRunning ? UiText.OscState(_oscRunningTransports, _oscRunningPort) : UiText.OscStopped;

    public string RecordButtonText => IsRecording ? UiText.StopRecord : UiText.Record;

    public bool IsConnectedFull => HasCamera && HasXu;

    public bool IsConnectedUvcOnly => HasCamera && !HasXu;

    public string ConnectionText => IsConnectedFull ? UiText.Connected : IsConnectedUvcOnly ? UiText.UvcOnly : UiText.Disconnected;

    public bool IsStatusError => StatusKind == StatusKind.Error;

    public bool IsStatusSuccess => StatusKind == StatusKind.Success;

    public bool IsStatusInfo => StatusKind is StatusKind.Info or StatusKind.Warning;

    public bool HasFrame => Frame is not null;

    public bool IsPreviewLive => PreviewState == PreviewState.Live;

    public bool CanTurnOnPreview => HasCamera && PreviewState == PreviewState.Off;

    /// <summary>The gimbal only responds while the camera streams video.</summary>
    public bool ShowGimbalHint => HasXu && !IsPreviewLive;

    public string StageTitle => !HasCamera ? UiText.NoCameraTitle
        : PreviewState switch
        {
            PreviewState.Off => UiText.PreviewOffTitle,
            PreviewState.Opening => UiText.PreviewOpening,
            PreviewState.Failed => UiText.PreviewFailedTitle,
            _ => UiText.PreviewWaiting,
        };

    public string StageDetail => !HasCamera ? UiText.NoCameraDetail
        : PreviewState switch
        {
            PreviewState.Off => UiText.PreviewOffDetail,
            PreviewState.Failed => PreviewError,
            _ => "",
        };

    /// <summary>Raised for saved photos/recordings and errors; the window shows them as toasts.</summary>
    public event EventHandler<StudioNotice>? Notice;

    public event EventHandler? SnapshotTaken;

    public static string FormatElapsed(TimeSpan t) =>
        string.Create(CultureInfo.InvariantCulture, $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}");

    public static string MediaFolder(Environment.SpecialFolder folder)
    {
        string root = Environment.GetFolderPath(folder);
        if (string.IsNullOrEmpty(root))
            root = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(root, "OpenGimbalCam Studio");
    }

    /// <summary>Copies the state worth persisting between runs.</summary>
    public void CaptureSettings(StudioSettings s)
    {
        s.CameraPath = SelectedCamera?.Path;
        s.PreviewEnabled = PreviewEnabled;
        s.Speed = Speed;
        s.OscPort = (int)(OscPort ?? OscServer.DefaultPort);
        s.OscUdp = OscUdp;
        s.OscTcp = OscTcp;
        s.OscObsbotCompatibility = OscObsbotCompatibility;
        s.InspectorTab = InspectorTabIndex;
        s.ShowGrid = ShowGrid;
    }

    private void NotifyStage()
    {
        OnPropertyChanged(nameof(StageTitle));
        OnPropertyChanged(nameof(StageDetail));
        OnPropertyChanged(nameof(CanTurnOnPreview));
        OnPropertyChanged(nameof(IsPreviewLive));
        OnPropertyChanged(nameof(ShowGimbalHint));
    }

    partial void OnHasCameraChanged(bool value) => NotifyStage();

    partial void OnHasXuChanged(bool value) => NotifyStage();

    partial void OnPreviewStateChanged(PreviewState value) => NotifyStage();

    partial void OnPreviewErrorChanged(string value) => NotifyStage();

    partial void OnFrameChanged(Bitmap? oldValue, Bitmap? newValue)
    {
        // The frame changes ~30 times per second; only its presence matters to the stage.
        if ((oldValue is null) != (newValue is null))
        {
            OnPropertyChanged(nameof(HasFrame));
            NotifyStage();
        }
    }

    partial void OnOscObsbotCompatibilityChanged(bool value)
    {
        if (_oscBridge is not null)
        {
            _oscBridge.ObsbotCompatibility = value;
            AddLog($"compatibilidade com o protocolo OSC do OBSBOT Center: {(value ? "ligada" : "desligada")}");
        }
    }

    partial void OnIsRecordingChanged(bool value)
    {
        if (value)
        {
            _recordWatch.Restart();
            RecordingElapsed = FormatElapsed(TimeSpan.Zero);
            _recordTimer ??= CreateTimer(500, () => RecordingElapsed = FormatElapsed(_recordWatch.Elapsed));
            _recordTimer.Start();
        }
        else
        {
            _recordTimer?.Stop();
            _recordWatch.Reset();
            RecordingElapsed = FormatElapsed(TimeSpan.Zero);
        }
    }

    private static DispatcherTimer CreateTimer(int milliseconds, Action tick)
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (_, _) => tick();
        return timer;
    }

    [RelayCommand]
    private void Refresh()
    {
        var previous = SelectedCamera?.Path;
        IReadOnlyList<CameraInfo> found;
        try
        {
            found = _platform.Enumerate();
        }
        catch (Exception ex)
        {
            Report(ex);
            return;
        }
        Cameras.Clear();
        foreach (var c in found)
            Cameras.Add(c);
        SelectedCamera = found.FirstOrDefault(c => previous is not null && c.Path == previous)
            ?? found.FirstOrDefault(c => _preferredCameraPath is not null && c.Path == _preferredCameraPath)
            ?? found.FirstOrDefault(c => c.IsSupportedTiny)
            ?? found.FirstOrDefault();
        if (found.Count == 0)
            SetStatus(UiText.NoCameraFound(_platform.Name), StatusKind.Warning);
    }

    partial void OnSelectedCameraChanged(CameraInfo? value) => OpenCamera(value);

    private void OpenCamera(CameraInfo? info)
    {
        CloseCamera();
        if (info is null)
            return;
        try
        {
            var camera = new ObsbotCamera(_platform, info);
            _camera = camera;
            var session = camera.Session;
            HasXu = session.ExtensionUnit is not null;

            _loading = true;
            Zoom = camera.GetZoomPercent() ?? 0;
            FovIndex = -1;
            Mirror = false;
            _loading = false;

            foreach (var c in Enum.GetValues<CameraControl>())
            {
                if (!GimbalControls.Contains(c) && session.GetRange(c) is { } r && r.Max > r.Min)
                    Controls.Add(new ControlViewModel(session, c, r, Report));
            }
            BuildControlGroups();
            UpdatePosition();
            HasCamera = true;
            if (PreviewEnabled)
                _ = StartPreviewAsync(info);
            SetStatus(HasXu
                ? UiText.CameraWithXu(info.Name, session.ExtensionUnit!.Location)
                : UiText.CameraUvcOnly(info.Name), StatusKind.Info);
        }
        catch (Exception ex)
        {
            _loading = false;
            Report(ex);
        }
    }

    private void BuildControlGroups()
    {
        ControlGroups.Clear();
        foreach (var group in Controls.GroupBy(c => c.Group).OrderBy(g => g.Key))
            ControlGroups.Add(new ControlGroupViewModel(ControlCatalog.GroupTitle(group.Key), group.OrderBy(c => c.Order).ToList()));
    }

    private void CloseCamera()
    {
        if (_moving)
            StopMove();
        _ = StopPreviewAsync();
        _camera?.Dispose();
        _camera = null;
        Controls.Clear();
        ControlGroups.Clear();
        HasCamera = false;
        HasXu = false;
        PanText = UiText.NoPosition;
        TiltText = UiText.NoPosition;
    }

    /// <summary>Press-and-hold on a direction button or arrow key; same signs as OSC SetGimbalUp/Down/Left/Right.</summary>
    public void StartMove(string direction)
    {
        double v = Speed / 100;
        var (pan, tilt) = direction switch
        {
            "up" => (0.0, v),
            "down" => (0.0, -v),
            "left" => (-v, 0.0),
            "right" => (v, 0.0),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null),
        };
        if (Run(() => _camera?.SetPanTiltSpeed(pan, tilt)))
        {
            _moving = true;
            _positionTimer ??= CreateTimer(250, UpdatePosition);
            _positionTimer.Start();
        }
    }

    public void StopMove()
    {
        if (!_moving)
            return;
        _moving = false;
        _positionTimer?.Stop();
        Run(() => _camera?.SetPanTiltSpeed(0, 0));
        UpdatePosition();
    }

    [RelayCommand]
    private void Home()
    {
        Run(() => _camera?.ResetPanTilt());
        UpdatePosition();
    }

    [RelayCommand]
    private void TriggerPreset(string id) => Run(() => _camera?.TriggerPreset(int.Parse(id, CultureInfo.InvariantCulture)));

    [RelayCommand]
    private void ZoomIn() => Zoom = Math.Clamp(Zoom + 10, 0, 100);

    [RelayCommand]
    private void ZoomOut() => Zoom = Math.Clamp(Zoom - 10, 0, 100);

    partial void OnZoomChanged(double value)
    {
        if (!_loading)
            Run(() => _camera?.SetZoomPercent((int)Math.Round(value)));
    }

    partial void OnFovIndexChanged(int value)
    {
        if (!_loading && value >= 0)
            Run(() => _camera?.SetFov(value));
    }

    partial void OnMirrorChanged(bool value)
    {
        if (!_loading)
            Run(() => _camera?.SetMirror(value));
    }

    private void UpdatePosition()
    {
        try
        {
            if (_camera?.GetPanTilt() is { } p)
            {
                PanText = $"{p.Pan}°";
                TiltText = $"{p.Tilt}°";
            }
            else
            {
                PanText = UiText.NoPosition;
                TiltText = UiText.NoPosition;
            }
        }
        catch (Exception ex)
        {
            Report(ex);
        }
    }

    [RelayCommand]
    private void RestoreImageDefaults()
    {
        foreach (var c in Controls)
            c.ResetCommand.Execute(null);
        SetStatus(UiText.ImageRestored, StatusKind.Success);
    }

    [RelayCommand]
    private void ShowOsc() => InspectorTabIndex = 1;

    [RelayCommand]
    private void ShowActivity() => InspectorTabIndex = 2;

    [RelayCommand]
    private void ToggleFocusMode() => IsFocusMode = !IsFocusMode;

    [RelayCommand]
    private void ExitFocusMode() => IsFocusMode = false;

    [RelayCommand]
    private void ToggleGrid() => ShowGrid = !ShowGrid;

    [RelayCommand]
    private void ClearLog() => Log.Clear();

    [RelayCommand]
    private void TurnOnPreview() => PreviewEnabled = true;

    partial void OnPreviewEnabledChanged(bool value)
    {
        if (value && SelectedCamera is { } camera && HasCamera)
            _ = StartPreviewAsync(camera);
        else if (!value)
            _ = StopPreviewAsync();
    }

    private async Task StartPreviewAsync(CameraInfo camera)
    {
        await StopPreviewAsync();
        int generation = _previewGeneration;
        PreviewState = PreviewState.Opening;
        PreviewInfo = UiText.PreviewOpening;
        try
        {
            var preview = await Task.Run(() => CameraPreview.StartAsync(camera, ShowFrame, CancellationToken.None));
            if (generation != _previewGeneration)
            {
                // The camera was changed or the preview turned off while opening.
                await preview.DisposeAsync();
                return;
            }
            _preview = preview;
            PreviewInfo = preview.Format;
            PreviewState = PreviewState.Live;
            AddLog($"vídeo: {camera.Name}, {preview.Format}");
        }
        catch (Exception ex)
        {
            if (generation == _previewGeneration)
            {
                PreviewInfo = "";
                PreviewError = ex.Message;
                PreviewState = PreviewState.Failed;
            }
            AddLog($"vídeo: {ex.Message}");
        }
    }

    private async Task StopPreviewAsync()
    {
        _previewGeneration++;
        if (_preview?.StopRecording() is { } done)
            OnRecordingSaved(done.Path, done.Frames);
        IsRecording = false;
        var preview = _preview;
        _preview = null;
        Frame = null;
        PreviewInfo = "";
        PreviewState = PreviewState.Off;
        if (preview is not null)
            await preview.DisposeAsync();
    }

    /// <summary>Called on the capture thread with a decoded frame.</summary>
    private void ShowFrame(Bitmap bitmap)
    {
        int generation = _previewGeneration;
        Dispatcher.UIThread.Post(() =>
        {
            if (generation != _previewGeneration)
            {
                bitmap.Dispose();
                return;
            }
            // Keep the frame being replaced alive until the next one: the renderer may still use it.
            _previousFrame?.Dispose();
            _previousFrame = Frame;
            Frame = bitmap;
            _preview?.FrameShown();
        });
    }

    [RelayCommand]
    private void Snapshot()
    {
        if (_preview is null)
        {
            SetStatus(UiText.OpenVideoForPhoto, StatusKind.Warning);
            return;
        }
        string? path = null;
        if (Run(() => path = _preview.SaveSnapshot(PhotosFolder)))
        {
            AddLog($"foto salva: {path}");
            SetStatus(UiText.PhotoSaved, StatusKind.Success);
            SnapshotTaken?.Invoke(this, EventArgs.Empty);
            Notice?.Invoke(this, new StudioNotice(UiText.PhotoSavedTitle, path!, StatusKind.Success, path));
        }
    }

    [RelayCommand]
    private void ToggleRecording()
    {
        if (_preview is null)
        {
            SetStatus(UiText.OpenVideoForRecording, StatusKind.Warning);
            return;
        }
        if (IsRecording)
        {
            if (_preview.StopRecording() is { } done)
                OnRecordingSaved(done.Path, done.Frames);
            IsRecording = false;
        }
        else
        {
            string? path = null;
            if (Run(() => path = _preview.StartRecording(VideosFolder, AddLog)))
            {
                AddLog($"gravando em {path}");
                IsRecording = true;
            }
        }
    }

    private void OnRecordingSaved(string path, int frames)
    {
        AddLog($"gravação salva: {path} ({frames} quadros)");
        Notice?.Invoke(this, new StudioNotice(UiText.RecordingSavedTitle, path, StatusKind.Success, path));
    }

    // OSC PCSnapshot / SetPCRecording: called from the server's threads; acts on the video shown here.
    void IPcMedia.Snapshot(int device) => OnUiForDevice(device, Snapshot);

    void IPcMedia.SetRecording(int device, bool on, bool highFps) => OnUiForDevice(device, () =>
    {
        if (on != IsRecording)
            ToggleRecording();
    });

    private void OnUiForDevice(int device, Action action) => Dispatcher.UIThread.Post(() =>
    {
        var cams = _oscHub?.Cameras;
        if (cams is null || device < 0 || device >= cams.Count
            || cams[device] is not ObsbotCamera c || c.Info.Path != SelectedCamera?.Path)
        {
            AddLog($"OSC: foto/gravação só para a câmera mostrada no vídeo (pedido para o índice {device})");
            return;
        }
        action();
    });

    [RelayCommand]
    private async Task ToggleOscAsync()
    {
        if (OscRunning)
        {
            await StopOscAsync();
            AddLog("servidor OSC parado");
            return;
        }
        if (!OscUdp && !OscTcp)
        {
            SetStatus(UiText.ChooseTransport, StatusKind.Warning);
            return;
        }

        int port = (int)(OscPort ?? OscServer.DefaultPort);
        try
        {
            _oscHub = new ObsbotCameraHub(_platform);
            var bridge = new OscBridge(_oscHub, AddLog, this) { ObsbotCompatibility = OscObsbotCompatibility };
            _oscBridge = bridge;
            AddLog($"compatibilidade com o protocolo OSC do OBSBOT Center: {(OscObsbotCompatibility ? "ligada" : "desligada")}");
            var endPoint = new IPEndPoint(IPAddress.Any, port);
            if (OscUdp)
                _oscServers.Add(new OscServer(OscTransport.Udp, endPoint, bridge.HandleAsync, AddLog));
            if (OscTcp)
                _oscServers.Add(new OscServer(OscTransport.Tcp, endPoint, bridge.HandleAsync, AddLog));
            foreach (var s in _oscServers)
            {
                s.Start();
                AddLog($"escutando OSC/{s.Transport.ToString().ToUpperInvariant()} em {s.LocalEndPoint}");
            }
            AddLog($"câmeras OBSBOT: {(_oscHub.Cameras.Count == 0 ? "nenhuma" : string.Join(", ", _oscHub.Cameras.Select((c, i) => $"[{i}] {c.Name}")))}");
            _oscRunningPort = port;
            _oscRunningTransports = OscUdp && OscTcp ? "UDP+TCP" : OscUdp ? "UDP" : "TCP";
            OscRunning = true;
        }
        catch (SocketException ex)
        {
            await StopOscAsync();
            SetStatus(ex.SocketErrorCode == SocketError.AddressAlreadyInUse ? UiText.PortInUse(port) : ex.Message, StatusKind.Error);
        }
    }

    private async Task StopOscAsync()
    {
        foreach (var s in _oscServers)
            await s.DisposeAsync();
        _oscServers.Clear();
        _oscBridge = null;
        _oscHub?.Dispose();
        _oscHub = null;
        OscRunning = false;
    }

    /// <summary>Thread-safe: the OSC server logs from socket callbacks.</summary>
    private void AddLog(string text)
    {
        var entry = new LogEntry(DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture), text,
            text.StartsWith("erro", StringComparison.OrdinalIgnoreCase));
        Dispatcher.UIThread.Post(() =>
        {
            Log.Add(entry);
            if (Log.Count > MaxLogLines)
                Log.RemoveAt(0);
        });
    }

    private void SetStatus(string text, StatusKind kind)
    {
        Status = text;
        StatusKind = kind;
    }

    private bool Run(Action action)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception ex)
        {
            Report(ex);
            return false;
        }
    }

    private void Report(Exception ex)
    {
        SetStatus($"Erro: {ex.Message}", StatusKind.Error);
        AddLog($"erro: {ex.Message}");
        long now = Environment.TickCount64;
        if (ex.Message != _lastErrorText || now - _lastErrorTick >= ErrorNoticeIntervalMs)
        {
            _lastErrorText = ex.Message;
            _lastErrorTick = now;
            Notice?.Invoke(this, new StudioNotice(UiText.ErrorTitle, ex.Message, StatusKind.Error));
        }
    }

    public async ValueTask DisposeAsync()
    {
        StopMove();
        _recordTimer?.Stop();
        _positionTimer?.Stop();
        await StopPreviewAsync();
        await StopOscAsync();
        CloseCamera();
    }
}
