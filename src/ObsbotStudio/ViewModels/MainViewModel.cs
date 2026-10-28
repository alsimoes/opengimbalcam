using System.Collections.ObjectModel;
using System.Net;
using System.Net.Sockets;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Obsbot.Core.Camera;
using Obsbot.Osc;
using Obsbot.Osc.Bridge;

namespace ObsbotStudio.ViewModels;

public sealed partial class MainViewModel : ObservableObject, IPcMedia, IDisposable
{
    private const int MaxLogLines = 500;

    // Gimbal, zoom and FOV have their own widgets; the relative ranges only scale speeds.
    private static readonly HashSet<CameraControl> GimbalControls =
        [CameraControl.Pan, CameraControl.Tilt, CameraControl.Zoom, CameraControl.PanRelative, CameraControl.TiltRelative];

    private readonly ICameraPlatform _platform;
    private ObsbotCamera? _camera;
    private bool _loading;
    private bool _moving;
    private ObsbotCameraHub? _oscHub;
    private readonly List<OscServer> _oscServers = [];
    private CameraPreview? _preview;
    private int _previewGeneration;
    private Bitmap? _previousFrame;

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
    private CameraInfo? _selectedCamera;

    [ObservableProperty]
    private string _status = "";

    [ObservableProperty]
    private bool _hasCamera;

    [ObservableProperty]
    private bool _hasXu;

    [ObservableProperty]
    private string _position = "";

    [ObservableProperty]
    private double _speed = 50;

    [ObservableProperty]
    private double _zoom;

    [ObservableProperty]
    private int _fovIndex = -1;

    [ObservableProperty]
    private bool _mirror;

    [ObservableProperty]
    private decimal? _oscPort = OscServer.DefaultPort;

    [ObservableProperty]
    private bool _oscUdp = true;

    [ObservableProperty]
    private bool _oscTcp;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OscButtonText))]
    private bool _oscRunning;

    public MainViewModel(ICameraPlatform platform)
    {
        _platform = platform;
        Refresh();
    }

    public ObservableCollection<CameraInfo> Cameras { get; } = [];

    public ObservableCollection<ControlViewModel> Controls { get; } = [];

    public ObservableCollection<string> Log { get; } = [];

    /// <summary>OSC SetView / libdev cameraSetFovU values 0, 1, 2.</summary>
    public IReadOnlyList<string> FovOptions { get; } = ["86° (amplo)", "78° (médio)", "65° (fechado)"];

    public string PlatformName => _platform.Name;

    public string OscButtonText => OscRunning ? "Parar servidor OSC" : "Iniciar servidor OSC";

    public string RecordButtonText => IsRecording ? "■ Parar" : "● Gravar";

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
            ?? found.FirstOrDefault(c => c.IsObsbot)
            ?? found.FirstOrDefault();
        if (found.Count == 0)
            Status = $"Nenhuma câmera encontrada ({_platform.Name}).";
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
            UpdatePosition();
            HasCamera = true;
            if (PreviewEnabled)
                _ = StartPreviewAsync(info);
            Status = HasXu
                ? $"{info.Name}: XU OBSBOT em {session.ExtensionUnit!.Location}."
                : $"{info.Name}: sem a Extension Unit OBSBOT; só controles UVC padrão.";
        }
        catch (Exception ex)
        {
            _loading = false;
            Report(ex);
        }
    }

    private void CloseCamera()
    {
        if (_moving)
            StopMove();
        _ = StopPreviewAsync();
        _camera?.Dispose();
        _camera = null;
        Controls.Clear();
        HasCamera = false;
        HasXu = false;
        Position = "";
    }

    /// <summary>Press-and-hold on a direction button; same signs as OSC SetGimbalUp/Down/Left/Right.</summary>
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
            _moving = true;
    }

    public void StopMove()
    {
        if (!_moving)
            return;
        _moving = false;
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
    private void TriggerPreset(string id) => Run(() => _camera?.TriggerPreset(int.Parse(id, System.Globalization.CultureInfo.InvariantCulture)));

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
            Position = _camera?.GetPanTilt() is { } p ? $"Pan {p.Pan}°  Tilt {p.Tilt}°" : "";
        }
        catch (Exception ex)
        {
            Report(ex);
        }
    }

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
        PreviewInfo = "abrindo vídeo…";
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
            AddLog($"vídeo: {camera.Name}, {preview.Format}");
        }
        catch (Exception ex)
        {
            if (generation == _previewGeneration)
                PreviewInfo = $"sem vídeo: {ex.Message}";
            AddLog($"vídeo: {ex.Message}");
        }
    }

    private async Task StopPreviewAsync()
    {
        _previewGeneration++;
        if (_preview?.StopRecording() is { } done)
            AddLog($"gravação salva: {done.Path} ({done.Frames} quadros)");
        IsRecording = false;
        var preview = _preview;
        _preview = null;
        Frame = null;
        PreviewInfo = "";
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
            Status = "Abra o vídeo para tirar uma foto.";
            return;
        }
        if (Run(() => AddLog($"foto salva: {_preview.SaveSnapshot(MediaFolder(Environment.SpecialFolder.MyPictures))}")))
            Status = "Foto salva.";
    }

    [RelayCommand]
    private void ToggleRecording()
    {
        if (_preview is null)
        {
            Status = "Abra o vídeo para gravar.";
            return;
        }
        if (IsRecording)
        {
            if (_preview.StopRecording() is { } done)
                AddLog($"gravação salva: {done.Path} ({done.Frames} quadros)");
            IsRecording = false;
        }
        else if (Run(() => AddLog($"gravando em {_preview.StartRecording(MediaFolder(Environment.SpecialFolder.MyVideos))}")))
        {
            IsRecording = true;
        }
    }

    private static string MediaFolder(Environment.SpecialFolder folder)
    {
        string root = Environment.GetFolderPath(folder);
        if (string.IsNullOrEmpty(root))
            root = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(root, "OBSBOT Studio");
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
            Status = "Escolha UDP e/ou TCP.";
            return;
        }

        int port = (int)(OscPort ?? OscServer.DefaultPort);
        try
        {
            _oscHub = new ObsbotCameraHub(_platform);
            var bridge = new OscBridge(_oscHub, AddLog, this);
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
            OscRunning = true;
        }
        catch (SocketException ex)
        {
            await StopOscAsync();
            Status = ex.SocketErrorCode == SocketError.AddressAlreadyInUse
                ? $"Porta {port} em uso. O OBSBOT Center está aberto com OSC ligado?"
                : ex.Message;
        }
    }

    private async Task StopOscAsync()
    {
        foreach (var s in _oscServers)
            await s.DisposeAsync();
        _oscServers.Clear();
        _oscHub?.Dispose();
        _oscHub = null;
        OscRunning = false;
    }

    /// <summary>Thread-safe: the OSC server logs from socket callbacks.</summary>
    private void AddLog(string text)
    {
        var line = $"{DateTime.Now:HH:mm:ss.fff} {text}";
        Dispatcher.UIThread.Post(() =>
        {
            Log.Add(line);
            if (Log.Count > MaxLogLines)
                Log.RemoveAt(0);
        });
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
        Status = $"Erro: {ex.Message}";
        AddLog($"erro: {ex.Message}");
    }

    public void Dispose()
    {
        CloseCamera();
        StopPreviewAsync().GetAwaiter().GetResult();
        StopOscAsync().GetAwaiter().GetResult();
    }
}
