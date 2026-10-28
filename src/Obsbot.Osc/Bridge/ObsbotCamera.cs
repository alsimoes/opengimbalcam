using Obsbot.Core.Camera;

namespace Obsbot.Osc.Bridge;

/// <summary>
/// One OBSBOT camera, driven the way libdev.dll does it: vendor commands through
/// XU selector 6/7 (<see cref="DirectCommands"/>), everything else through the
/// standard UVC controls of the platform backend.
/// </summary>
public sealed class ObsbotCamera : ICamera
{
    private readonly ICameraPlatform _platform;
    private ICameraSession? _session;
    private (int Pan, int Tilt)? _maxSpeed;

    public ObsbotCamera(ICameraPlatform platform, CameraInfo info)
    {
        _platform = platform;
        Info = info;
    }

    public CameraInfo Info { get; }

    public string Name => Info.Name;

    // DeviceInfo's last argument selects the TouchOSC sample's page: 0, 1 or 6 = Tiny page.
    public int ProductCode => Info.Name.Contains("Tiny", StringComparison.OrdinalIgnoreCase) ? 0 : 2;

    private ICameraSession Session => _session ??= _platform.Open(Info);

    private IExtensionUnit Xu => Session.RequireXu();

    public void SetPanTiltSpeed(double pan, double tilt)
    {
        // libdev: clamp to [-1, 1], multiply by the max of the UVC PanRelative/TiltRelative
        // range and truncate (cameraSetPanTiltRelative, RVA 0x11BF0).
        var (maxPan, maxTilt) = _maxSpeed ??= (
            Session.GetRange(CameraControl.PanRelative)?.Max ?? 0,
            Session.GetRange(CameraControl.TiltRelative)?.Max ?? 0);
        Xu.Set((uint)ObsbotXu.Selector.Command, DirectCommands.PanTiltRelative(ScaleSpeed(pan, maxPan), ScaleSpeed(tilt, maxTilt)));
    }

    /// <summary>libdev's speed conversion: trunc(clamp(v, -1, 1) * range max).</summary>
    internal static int ScaleSpeed(double v, int rangeMax)
    {
        // usbvideo reports the speed byte sign-extended (the Tiny's 0xA0 shows as -96).
        if (rangeMax < 0)
            rangeMax &= 0xFF;
        if (rangeMax is <= 0 or > 0xFF)
            rangeMax = 100;
        return (int)(Math.Clamp(v, -1, 1) * rangeMax);
    }

    public void ResetPanTilt()
    {
        Session.Set(CameraControl.Pan, 0);
        Session.Set(CameraControl.Tilt, 0);
    }

    public (int Pan, int Tilt)? GetPanTilt() =>
        Session.Get(CameraControl.Pan) is { } p && Session.Get(CameraControl.Tilt) is { } t
            ? (p.Value, t.Value)
            : null;

    public int? GetZoomPercent()
    {
        if (Session.GetRange(CameraControl.Zoom) is not { } r || Session.Get(CameraControl.Zoom) is not { } v)
            return null;
        return r.Max == r.Min ? 0 : (int)Math.Round((v.Value - r.Min) * 100.0 / (r.Max - r.Min));
    }

    public void SetZoomPercent(int percent)
    {
        var r = Session.GetRange(CameraControl.Zoom) ?? throw new NotSupportedException($"{Name}: sem controle de zoom UVC.");
        Session.Set(CameraControl.Zoom, FromPercent(r, percent));
    }

    public void SetFov(int fovType) =>
        Xu.Set((uint)ObsbotXu.Selector.Command, DirectCommands.Find("fov")!.Build([fovType]));

    public void SetMirror(bool on) =>
        Xu.Set((uint)ObsbotXu.Selector.Command, DirectCommands.Find("flip-horizontal")!.Build([on ? 1 : 0]));

    public void TriggerPreset(int presetId) =>
        Xu.Set((uint)ObsbotXu.Selector.GimbalPreset, DirectCommands.GimbalPreset(GimbalPresetAction.Trigger, presetId));

    public void SetAuto(UvcControl control, bool auto)
    {
        var current = Get(control) ?? throw Unsupported(control);
        Set(control, current.Value, auto ? ControlFlags.Auto : ControlFlags.Manual);
    }

    public void SetPercent(UvcControl control, int percent)
    {
        var range = Range(control) ?? throw Unsupported(control);
        Set(control, FromPercent(range, percent), ControlFlags.Manual);
    }

    public void SetRaw(UvcControl control, int value)
    {
        var range = Range(control) ?? throw Unsupported(control);
        Set(control, Math.Clamp(value, range.Min, range.Max), ControlFlags.Manual);
    }

    public void Dispose()
    {
        _session?.Dispose();
        _session = null;
    }

    internal static int FromPercent(ControlRange r, int percent)
    {
        double v = r.Min + Math.Clamp(percent, 0, 100) / 100.0 * (r.Max - r.Min);
        int step = Math.Max(r.Step, 1);
        return Math.Clamp(r.Min + (int)Math.Round((v - r.Min) / step, MidpointRounding.AwayFromZero) * step, r.Min, r.Max);
    }

    private NotSupportedException Unsupported(UvcControl c) => new($"{Name}: controle UVC {c} indisponível.");

    private ControlRange? Range(UvcControl c) => Session.GetRange(ToCameraControl(c));

    private ControlValue? Get(UvcControl c) => Session.Get(ToCameraControl(c));

    private void Set(UvcControl c, int value, ControlFlags flags) => Session.Set(ToCameraControl(c), value, flags);

    private static CameraControl ToCameraControl(UvcControl c) => c switch
    {
        UvcControl.Focus => CameraControl.Focus,
        UvcControl.Exposure => CameraControl.Exposure,
        UvcControl.WhiteBalance => CameraControl.WhiteBalance,
        UvcControl.Contrast => CameraControl.Contrast,
        UvcControl.Saturation => CameraControl.Saturation,
        UvcControl.Sharpness => CameraControl.Sharpness,
        UvcControl.Hue => CameraControl.Hue,
        _ => throw new ArgumentOutOfRangeException(nameof(c), c, null),
    };
}

/// <summary>All OBSBOT cameras (VID 3564) in enumeration order, at most four as in OBSBOT Center.</summary>
public sealed class ObsbotCameraHub : ICameraHub, IDisposable
{
    public const int MaxDevices = 4;

    private readonly ICameraPlatform _platform;
    private List<ObsbotCamera> _cameras = [];

    public ObsbotCameraHub(ICameraPlatform platform)
    {
        _platform = platform;
        Refresh();
    }

    public IReadOnlyList<ICamera> Cameras => _cameras;

    public void Refresh()
    {
        Dispose();
        _cameras = _platform.Enumerate().Where(d => d.IsObsbot).Take(MaxDevices).Select(d => new ObsbotCamera(_platform, d)).ToList();
    }

    public void Dispose()
    {
        foreach (var c in _cameras)
            c.Dispose();
        _cameras = [];
    }
}
