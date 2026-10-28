using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Obsbot.Core.Camera;

namespace Obsbot.Devices.Linux;

/// <summary>
/// An opened /dev/videoN. Values use the same units as the Windows backend, so code
/// above works unchanged: pan/tilt in degrees (V4L2 uses arc seconds) and "auto" as a
/// flag (V4L2 has a separate control for it).
/// </summary>
[SupportedOSPlatform("linux")]
internal sealed unsafe class V4l2Session : ICameraSession
{
    private const uint CtrlFlagDisabled = 0x0001;
    private const int ArcSecondsPerDegree = 3600;

    private readonly byte[] _descriptors;
    private int _fd;
    private UvcExtensionUnit? _xu;
    private bool _xuSearched;

    internal V4l2Session(CameraInfo info, int fd, byte[] descriptors)
    {
        Info = info;
        _fd = fd;
        _descriptors = descriptors;
    }

    public CameraInfo Info { get; }

    private int Fd => _fd >= 0 ? _fd : throw new ObjectDisposedException(nameof(V4l2Session));

    public ControlRange? GetRange(CameraControl control)
    {
        if (Map(control) is not { } m || Query(m.Id) is not { } q)
            return null;
        var caps = ControlFlags.Manual | (m.AutoId is { } a && Query(a) is not null ? ControlFlags.Auto : 0);
        int scale = m.Scale;
        return new ControlRange(q.Minimum / scale, q.Maximum / scale, Math.Max(q.Step / scale, 1), q.DefaultValue / scale, caps);
    }

    public ControlValue? Get(CameraControl control)
    {
        if (Map(control) is not { } m || GetCtrl(m.Id) is not { } v)
            return null;
        bool auto = m.AutoId is { } a && GetCtrl(a) is { } av && av == m.AutoOn;
        return new ControlValue(v / m.Scale, auto ? ControlFlags.Auto : ControlFlags.Manual);
    }

    public void Set(CameraControl control, int value, ControlFlags flags = ControlFlags.Manual)
    {
        var m = Map(control) ?? throw new NotSupportedException($"{control} não tem equivalente V4L2.");
        if (m.AutoId is { } autoId && Query(autoId) is not null)
        {
            SetCtrl(autoId, flags.HasFlag(ControlFlags.Auto) ? m.AutoOn : m.AutoOff, $"{control} auto");
            if (flags.HasFlag(ControlFlags.Auto))
                return;
        }
        else if (flags.HasFlag(ControlFlags.Auto))
        {
            throw new NotSupportedException($"{control} não tem modo automático.");
        }
        SetCtrl(m.Id, checked(value * m.Scale), control.ToString());
    }

    public IExtensionUnit? ExtensionUnit
    {
        get
        {
            if (!_xuSearched)
            {
                _xuSearched = true;
                if (UvcDescriptors.FindExtensionUnit(_descriptors, ObsbotXu.Guid) is { } unit)
                    _xu = new UvcExtensionUnit(Fd, unit, ObsbotXu.Guid);
            }
            return _xu;
        }
    }

    public IReadOnlyList<string> DescribeTopology()
    {
        var lines = new List<string> { "Unidades UVC (descritores USB, como o uvcvideo as vê):" };
        if (_descriptors.Length == 0)
            lines.Add("  descritores indisponíveis em sysfs");
        foreach (var u in UvcDescriptors.ParseUnits(_descriptors))
        {
            var tag = u.Guid == ObsbotXu.Guid ? "  <- XU OBSBOT" : "";
            lines.Add($"  unidade {u.Id,2}: {u.Kind}{(u.Guid is { } g ? $" {g:B}" : "")}{tag}");
        }
        return lines;
    }

    public void Dispose()
    {
        if (_fd >= 0)
        {
            LibC.Close(_fd);
            _fd = -1;
        }
    }

    private readonly record struct Mapping(uint Id, int Scale = 1, uint? AutoId = null, int AutoOn = 1, int AutoOff = 0);

    private static Mapping? Map(CameraControl c) => c switch
    {
        CameraControl.Pan => new(V4l2Cid.PanAbsolute, ArcSecondsPerDegree),
        CameraControl.Tilt => new(V4l2Cid.TiltAbsolute, ArcSecondsPerDegree),
        CameraControl.Zoom => new(V4l2Cid.ZoomAbsolute),
        CameraControl.Exposure => new(V4l2Cid.ExposureAbsolute, AutoId: V4l2Cid.ExposureAuto,
            AutoOn: V4l2Cid.ExposureAperturePriority, AutoOff: V4l2Cid.ExposureManual),
        CameraControl.Iris => new(V4l2Cid.IrisAbsolute),
        CameraControl.Focus => new(V4l2Cid.FocusAbsolute, AutoId: V4l2Cid.FocusAuto),
        CameraControl.PanRelative => new(V4l2Cid.PanSpeed),
        CameraControl.TiltRelative => new(V4l2Cid.TiltSpeed),
        CameraControl.Brightness => new(V4l2Cid.Brightness),
        CameraControl.Contrast => new(V4l2Cid.Contrast),
        CameraControl.Hue => new(V4l2Cid.Hue),
        CameraControl.Saturation => new(V4l2Cid.Saturation),
        CameraControl.Sharpness => new(V4l2Cid.Sharpness),
        CameraControl.Gamma => new(V4l2Cid.Gamma),
        CameraControl.WhiteBalance => new(V4l2Cid.WhiteBalanceTemperature, AutoId: V4l2Cid.AutoWhiteBalance),
        CameraControl.BacklightCompensation => new(V4l2Cid.BacklightCompensation),
        CameraControl.Gain => new(V4l2Cid.Gain),
        _ => null, // Roll: uvcvideo has no mapping
    };

    private V4l2QueryCtrl? Query(uint id)
    {
        var q = new V4l2QueryCtrl { Id = id };
        return LibC.Ioctl(Fd, LibC.VIDIOC_QUERYCTRL, &q) == 0 && (q.Flags & CtrlFlagDisabled) == 0 ? q : null;
    }

    private int? GetCtrl(uint id)
    {
        var c = new V4l2Control { Id = id };
        return LibC.Ioctl(Fd, LibC.VIDIOC_G_CTRL, &c) == 0 ? c.Value : null;
    }

    private void SetCtrl(uint id, int value, string what)
    {
        var c = new V4l2Control { Id = id, Value = value };
        LibC.IoctlOrThrow(Fd, LibC.VIDIOC_S_CTRL, &c, $"VIDIOC_S_CTRL {what}={value}");
    }
}

/// <summary>
/// The OBSBOT XU through UVCIOC_CTRL_QUERY. uvcvideo insists on the control's exact
/// size (UVC GET_LEN), so buffers are sized per selector rather than libdev's fixed 60.
/// </summary>
[SupportedOSPlatform("linux")]
internal sealed unsafe class UvcExtensionUnit : IExtensionUnit
{
    private readonly int _fd;
    private readonly byte _unit;
    private readonly Dictionary<uint, int> _lengths = [];

    internal UvcExtensionUnit(int fd, byte unit, Guid guid)
    {
        _fd = fd;
        _unit = unit;
        Guid = guid;
    }

    public Guid Guid { get; }

    public string Location => $"unidade UVC {_unit}";

    public byte[] Get(uint selector, int length = ObsbotXu.PayloadLength)
    {
        var buffer = new byte[ControlLength(selector, length)];
        Query(selector, UvcQuery.GetCur, buffer, "GET");
        return buffer.Length > length ? buffer[..length] : buffer;
    }

    public void Set(uint selector, ReadOnlySpan<byte> payload, int length = ObsbotXu.PayloadLength)
    {
        var buffer = new byte[ControlLength(selector, length)];
        if (payload.Length > buffer.Length)
            throw new ArgumentException($"Payload de {payload.Length} bytes excede {buffer.Length}.", nameof(payload));
        payload.CopyTo(buffer);
        Query(selector, UvcQuery.SetCur, buffer, "SET");
    }

    public void Dispose()
    {
        // The file descriptor belongs to the session.
    }

    private int ControlLength(uint selector, int fallback)
    {
        if (_lengths.TryGetValue(selector, out var n))
            return n;
        var len = new byte[2];
        try
        {
            Query(selector, UvcQuery.GetLen, len, "GET_LEN");
            n = len[0] | (len[1] << 8);
        }
        catch (XuException)
        {
            n = 0;
        }
        n = n > 0 ? n : fallback;
        _lengths[selector] = n;
        return n;
    }

    private void Query(uint selector, byte query, byte[] buffer, string operation)
    {
        fixed (byte* data = buffer)
        {
            var q = new UvcXuControlQuery
            {
                Unit = _unit,
                Selector = checked((byte)selector),
                Query = query,
                Size = checked((ushort)buffer.Length),
                Data = (IntPtr)data,
            };
            if (LibC.Ioctl(_fd, LibC.UVCIOC_CTRL_QUERY, &q) < 0)
            {
                int errno = Marshal.GetLastPInvokeError();
                throw new XuException(selector, operation, errno, $"{Marshal.GetLastPInvokeErrorMessage()} (errno {errno})");
            }
        }
    }
}
