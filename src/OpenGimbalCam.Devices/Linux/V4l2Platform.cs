using System.Globalization;
using System.Runtime.Versioning;
using OpenGimbalCam.Core.Camera;

namespace OpenGimbalCam.Devices.Linux;

/// <summary>
/// Video capture devices from /sys/class/video4linux. uvcvideo creates two nodes per
/// camera (capture and metadata); only the one with <c>index</c> 0 is listed.
/// Opening /dev/videoN needs read/write access (usually the <c>video</c> group).
/// </summary>
[SupportedOSPlatform("linux")]
public sealed class V4l2Platform : ICameraPlatform
{
    private const string SysClass = "/sys/class/video4linux";

    public string Name => "Linux (V4L2 + uvcvideo)";

    public IReadOnlyList<CameraInfo> Enumerate()
    {
        var result = new List<CameraInfo>();
        if (!Directory.Exists(SysClass))
            return result;

        var nodes = Directory.GetDirectories(SysClass, "video*")
            .Select(d => (Dir: d, Number: int.TryParse(Path.GetFileName(d)[5..], out var n) ? n : int.MaxValue))
            .OrderBy(x => x.Number);
        foreach (var (dir, _) in nodes)
        {
            if (ReadSys(Path.Combine(dir, "index")) is { } idx && idx != "0")
                continue;
            var usb = UsbDeviceDir(dir);
            result.Add(new CameraInfo(
                result.Count,
                ReadSys(Path.Combine(dir, "name")) ?? Path.GetFileName(dir),
                "/dev/" + Path.GetFileName(dir),
                ReadHex(usb, "idVendor"),
                ReadHex(usb, "idProduct")));
        }
        return result;
    }

    public ICameraSession Open(CameraInfo camera)
    {
        var path = camera.Path ?? throw new ArgumentException("câmera sem caminho /dev/videoN", nameof(camera));
        int fd = LibC.Open(path, LibC.O_RDWR | LibC.O_NONBLOCK);
        if (fd < 0)
        {
            throw new IOException($"{path}: {System.Runtime.InteropServices.Marshal.GetLastPInvokeErrorMessage()}"
                + " (o usuário está no grupo 'video'?)");
        }
        var usb = UsbDeviceDir(Path.Combine(SysClass, Path.GetFileName(path)));
        byte[] descriptors = usb is not null && File.Exists(Path.Combine(usb, "descriptors"))
            ? File.ReadAllBytes(Path.Combine(usb, "descriptors"))
            : [];
        return new V4l2Session(camera, fd, descriptors);
    }

    /// <summary>
    /// videoN/device is the USB interface (e.g. 1-2:1.0); its parent is the USB device,
    /// which holds idVendor, idProduct and the raw descriptors.
    /// </summary>
    private static string? UsbDeviceDir(string videoDir)
    {
        var iface = Path.Combine(videoDir, "device");
        if (!Directory.Exists(iface))
            return null;
        var resolved = new DirectoryInfo(iface).ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? iface;
        var parent = Directory.GetParent(resolved)?.FullName;
        return parent is not null && File.Exists(Path.Combine(parent, "idVendor")) ? parent : null;
    }

    private static string? ReadSys(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static int? ReadHex(string? dir, string attribute) =>
        dir is not null && ReadSys(Path.Combine(dir, attribute)) is { } s
            && int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v)
            ? v
            : null;
}
