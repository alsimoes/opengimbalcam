namespace Obsbot.Core.Camera;

/// <summary>
/// The standard UVC controls, named after the DirectShow properties libdev.dll uses
/// (IAMCameraControl / IAMVideoProcAmp). Each platform maps them to its own API:
/// DirectShow on Windows, V4L2 control ids on Linux.
/// </summary>
public enum CameraControl
{
    // UVC Camera Terminal
    Pan,
    Tilt,
    Roll,
    Zoom,
    Exposure,
    Iris,
    Focus,

    /// <summary>Range only: its max scales libdev's cameraSetPanTiltRelative speed.</summary>
    PanRelative,

    /// <summary>Range only, see <see cref="PanRelative"/>.</summary>
    TiltRelative,

    // UVC Processing Unit
    Brightness,
    Contrast,
    Hue,
    Saturation,
    Sharpness,
    Gamma,
    WhiteBalance,
    BacklightCompensation,
    Gain,

    /// <summary>Anti-flicker: 0 = off, 1 = 50 Hz, 2 = 60 Hz (libdev cameraSetAntiFlickR on UVC).</summary>
    PowerlineFrequency,
}

[Flags]
public enum ControlFlags
{
    None = 0,
    Auto = 1,
    Manual = 2,
}

public readonly record struct ControlRange(int Min, int Max, int Step, int Default, ControlFlags Caps);

public readonly record struct ControlValue(int Value, ControlFlags Flags);

/// <summary>A video capture device as listed by the platform.</summary>
/// <param name="Index">Position in the platform's enumeration (the CLI's <c>-d</c>).</param>
/// <param name="Path">DirectShow device path on Windows, /dev/videoN on Linux.</param>
public sealed record CameraInfo(int Index, string Name, string? Path, int? VendorId, int? ProductId)
{
    /// <summary>USB vendor id of Remo Tech (OBSBOT); libdev matches "VID_3564&amp;PID_xxxx".</summary>
    public const int ObsbotVendorId = 0x3564;

    public bool IsObsbot => VendorId == ObsbotVendorId;

    public string UsbId => VendorId is { } v ? $"{v:X4}:{ProductId:X4}" : "sem VID/PID";
}

/// <summary>
/// Raw GET_CUR/SET_CUR access to a UVC Extension Unit (libdev's uvcExtGet / uvcExtSet).
/// </summary>
public interface IExtensionUnit : IDisposable
{
    Guid Guid { get; }

    /// <summary>Where the unit was found, e.g. "nó KS 3" or "unidade UVC 6".</summary>
    string Location { get; }

    byte[] Get(uint selector, int length = ObsbotXu.PayloadLength);

    /// <summary>SET_CUR; the payload is zero-padded to <paramref name="length"/>.</summary>
    void Set(uint selector, ReadOnlySpan<byte> payload, int length = ObsbotXu.PayloadLength);
}

/// <summary>An opened camera: standard UVC controls plus the OBSBOT Extension Unit.</summary>
public interface ICameraSession : IDisposable
{
    CameraInfo Info { get; }

    ControlRange? GetRange(CameraControl control);

    ControlValue? Get(CameraControl control);

    void Set(CameraControl control, int value, ControlFlags flags = ControlFlags.Manual);

    /// <summary>The OBSBOT XU, located as libdev does; null when no unit answers for its GUID.</summary>
    IExtensionUnit? ExtensionUnit { get; }

    /// <summary>Human-readable description of the UVC topology (terminals, units, XUs).</summary>
    IReadOnlyList<string> DescribeTopology();
}

/// <summary>Camera discovery for one operating system.</summary>
public interface ICameraPlatform
{
    /// <summary>e.g. "Windows (DirectShow + Kernel Streaming)".</summary>
    string Name { get; }

    IReadOnlyList<CameraInfo> Enumerate();

    ICameraSession Open(CameraInfo camera);
}

public sealed class XuException : Exception
{
    public XuException(uint selector, string operation, int code, string? detail = null)
        : base($"XU {operation} seletor {selector} falhou: {detail ?? $"0x{code:X8}"}")
    {
        Selector = selector;
        HResult = code;
    }

    public uint Selector { get; }
}

public static class CameraSessionExtensions
{
    /// <summary>The OBSBOT XU or a descriptive error.</summary>
    public static IExtensionUnit RequireXu(this ICameraSession session) =>
        session.ExtensionUnit ?? throw new NotSupportedException(
            $"{session.Info.Name}: a Extension Unit OBSBOT ({ObsbotXu.Guid:B}) não respondeu.");

    /// <summary>Sends a direct command (XU selector 6).</summary>
    public static void SendCommand(this ICameraSession session, byte[] payload) =>
        session.RequireXu().Set((uint)ObsbotXu.Selector.Command, payload);
}
