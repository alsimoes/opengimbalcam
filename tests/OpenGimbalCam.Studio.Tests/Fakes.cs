using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using OpenGimbalCam.Core.Camera;

namespace OpenGimbalCam.Studio.Tests;

internal sealed class FakeCameraPlatform(bool withCamera) : ICameraPlatform
{
    public static readonly CameraInfo Tiny = new(0, "OBSBOT Tiny (simulada)", @"\\?\usb#vid_3564&pid_fef0#fake", 0x3564, 0xFEF0);

    public string Name => "Simulada";

    public FakeSession? LastSession { get; private set; }

    public IReadOnlyList<CameraInfo> Enumerate() => withCamera ? [Tiny] : [];

    public ICameraSession Open(CameraInfo camera) => LastSession = new FakeSession(camera);
}

internal sealed class FakeSession : ICameraSession
{
    private static readonly ControlFlags AutoManual = ControlFlags.Auto | ControlFlags.Manual;

    private readonly Dictionary<CameraControl, ControlRange> _ranges = new()
    {
        [CameraControl.Exposure] = new(-13, -2, 1, -6, AutoManual),
        [CameraControl.WhiteBalance] = new(2800, 6500, 100, 4600, AutoManual),
        [CameraControl.PowerlineFrequency] = new(0, 2, 1, 2, ControlFlags.Manual),
        [CameraControl.BacklightCompensation] = new(0, 1, 1, 0, ControlFlags.Manual),
        [CameraControl.Pan] = new(-468000, 468000, 3600, 0, ControlFlags.Manual),
        [CameraControl.Tilt] = new(-324000, 324000, 3600, 0, ControlFlags.Manual),
        [CameraControl.Zoom] = new(0, 100, 1, 0, ControlFlags.Manual),
        [CameraControl.PanRelative] = new(-1, 1, 1, 0, ControlFlags.Manual),
        [CameraControl.TiltRelative] = new(-1, 1, 1, 0, ControlFlags.Manual),
        [CameraControl.Brightness] = new(0, 255, 1, 128, ControlFlags.Manual),
        [CameraControl.Contrast] = new(0, 255, 1, 128, ControlFlags.Manual),
        [CameraControl.Hue] = new(-40, 40, 1, 0, ControlFlags.Manual),
        [CameraControl.Saturation] = new(0, 255, 1, 128, ControlFlags.Manual),
        [CameraControl.Sharpness] = new(0, 255, 1, 128, ControlFlags.Manual),
        [CameraControl.Gamma] = new(72, 500, 1, 100, ControlFlags.Manual),
        [CameraControl.Gain] = new(0, 255, 1, 0, ControlFlags.Manual),
    };

    private readonly Dictionary<CameraControl, ControlValue> _values;

    public FakeSession(CameraInfo info)
    {
        Info = info;
        _values = _ranges.ToDictionary(kv => kv.Key, kv => new ControlValue(kv.Value.Default, kv.Value.Caps.HasFlag(ControlFlags.Auto) ? ControlFlags.Auto : ControlFlags.Manual));
        _values[CameraControl.Brightness] = new(100, ControlFlags.Manual);
        _values[CameraControl.WhiteBalance] = new(5200, ControlFlags.Manual);
        _values[CameraControl.PowerlineFrequency] = new(1, ControlFlags.Manual);
    }

    public CameraInfo Info { get; }

    public List<(CameraControl Control, int Value, ControlFlags Flags)> Writes { get; } = [];

    public ControlRange? GetRange(CameraControl control) => _ranges.TryGetValue(control, out var r) ? r : null;

    public ControlValue? Get(CameraControl control) => _values.TryGetValue(control, out var v) ? v : null;

    public void Set(CameraControl control, int value, ControlFlags flags = ControlFlags.Manual)
    {
        Writes.Add((control, value, flags));
        _values[control] = new ControlValue(value, flags);
    }

    public IExtensionUnit? ExtensionUnit { get; } = new FakeXu();

    public IReadOnlyList<string> DescribeTopology() => [];

    public void Dispose()
    {
    }
}

internal sealed class FakeXu : IExtensionUnit
{
    public Guid Guid => Guid.Empty;

    public string Location => "unidade simulada";

    public byte[] Get(uint selector, int length = ObsbotXu.PayloadLength) => new byte[length];

    public void Set(uint selector, ReadOnlySpan<byte> payload, int length = ObsbotXu.PayloadLength)
    {
    }

    public void Dispose()
    {
    }
}

internal static class FakeFrames
{
    /// <summary>A 1280x720 gradient standing in for the camera image.</summary>
    public static WriteableBitmap Gradient()
    {
        var bitmap = new WriteableBitmap(new PixelSize(1280, 720), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        using var fb = bitmap.Lock();
        var row = new byte[fb.RowBytes];
        for (int y = 0; y < 720; y++)
        {
            for (int x = 0; x < 1280; x++)
            {
                row[x * 4] = (byte)(60 + 140 * y / 720);
                row[x * 4 + 1] = (byte)(40 + 100 * x / 1280);
                row[x * 4 + 2] = (byte)(90 + 120 * x / 1280);
                row[x * 4 + 3] = 255;
            }
            System.Runtime.InteropServices.Marshal.Copy(row, 0, fb.Address + y * fb.RowBytes, row.Length);
        }
        return bitmap;
    }
}
