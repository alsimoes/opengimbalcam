using OpenGimbalCam.Core.Camera;
using OpenGimbalCam.Core.Protocol;

namespace OpenGimbalCam.Osc.Bridge;

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
    private ushort _sequence = (ushort)Random.Shared.Next(1, 0x8000);

    public ObsbotCamera(ICameraPlatform platform, CameraInfo info)
    {
        _platform = platform;
        Info = info;
    }

    public CameraInfo Info { get; }

    public string Name => Info.Name;

    /// <summary>libdev's ObsbotProductType of the Tiny (PID FEF0), the only model this project targets.</summary>
    public int ProductCode => 0;

    /// <summary>The underlying session, opened on first use.</summary>
    public ICameraSession Session => _session ??= _platform.Open(Info);

    private IExtensionUnit Xu => Session.RequireXu();

    public void SetPanTiltSpeed(double pan, double tilt)
    {
        // libdev: clamp to [-1, 1], multiply by the max of the UVC PanRelative/TiltRelative
        // range and truncate (cameraSetPanTiltRelative, RVA 0x11BF0).
        var panRange = Session.GetRange(CameraControl.PanRelative);
        var tiltRange = Session.GetRange(CameraControl.TiltRelative);
        var (maxPan, maxTilt) = _maxSpeed ??= (panRange?.Max ?? 0, tiltRange?.Max ?? 0);
        int p = ScaleSpeed(pan, maxPan), t = ScaleSpeed(tilt, maxTilt);

        if (panRange is not null && tiltRange is not null)
        {
            // Tested on the Tiny: the standard UVC CT_PANTILT_RELATIVE control moves the gimbal,
            // while XU selector 6 (09 04 …) is accepted but ignored (OBSBOT Center only sends it
            // to product types 5/6/10/13/25). A positive PanRelative lowers the absolute pan, so
            // the sign is flipped to keep "pan +" = absolute pan increasing.
            Session.Set(CameraControl.PanRelative, PanRelativeValue(p));
            Session.Set(CameraControl.TiltRelative, t);
        }
        else
        {
            Xu.Set((uint)ObsbotXu.Selector.Command, DirectCommands.PanTiltRelative(p, t));
        }
    }

    /// <summary>UVC PanRelative value that moves towards increasing absolute pan for speed <paramref name="speed"/>.</summary>
    public static int PanRelativeValue(int speed) => -speed;

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

    public bool? IsAuto(UvcControl control) =>
        Get(control) is { } v ? v.Flags.HasFlag(ControlFlags.Auto) : null;

    public void SetShutterSpeed(int denominator)
    {
        var range = Range(UvcControl.Exposure) ?? throw Unsupported(UvcControl.Exposure);
        Set(UvcControl.Exposure, Math.Clamp(ExposureValue(range, denominator), range.Min, range.Max), ControlFlags.Manual);
    }

    /// <summary>
    /// Exposure for 1/<paramref name="denominator"/> s. DirectShow reports log2 seconds
    /// (the Tiny: -13..-2), V4L2 exposure_absolute is in 100 µs units; a negative
    /// minimum tells them apart.
    /// </summary>
    internal static int ExposureValue(ControlRange range, int denominator)
    {
        double seconds = 1.0 / Math.Max(denominator, 1);
        return range.Min < 0
            ? (int)Math.Round(Math.Log2(seconds))
            : (int)Math.Round(seconds * 10_000);
    }

    public void SendCommand(string name, params int[] args)
    {
        var command = DirectCommands.Find(name) ?? throw new ArgumentException($"comando direto desconhecido: {name}", nameof(name));
        Xu.Set((uint)ObsbotXu.Selector.Command, command.Build(args));
    }

    public void SendMessage(RemoRequest request)
    {
        var frame = RemoV1.ToWire(request, unchecked(_sequence++)).Encode();
        Xu.Set((uint)ObsbotXu.Selector.Message, frame);
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
        UvcControl.BacklightCompensation => CameraControl.BacklightCompensation,
        UvcControl.Gain => CameraControl.Gain,
        UvcControl.PowerlineFrequency => CameraControl.PowerlineFrequency,
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
