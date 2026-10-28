using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Obsbot.Devices.Linux;

// The parts of <linux/videodev2.h> and <linux/uvcvideo.h> needed to reach an OBSBOT
// camera through the uvcvideo driver: standard controls via VIDIOC_*CTRL and the
// vendor Extension Unit via UVCIOC_CTRL_QUERY (the Linux counterpart of KSP_NODE).

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct V4l2QueryCtrl
{
    public uint Id;
    public uint Type;
    public fixed byte Name[32];
    public int Minimum;
    public int Maximum;
    public int Step;
    public int DefaultValue;
    public uint Flags;
    public fixed uint Reserved[2];
}

[StructLayout(LayoutKind.Sequential)]
internal struct V4l2Control
{
    public uint Id;
    public int Value;
}

/// <summary>struct uvc_xu_control_query: 16 bytes on 64-bit, 12 on 32-bit (pointer member).</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct UvcXuControlQuery
{
    public byte Unit;
    public byte Selector;
    public byte Query;
    public ushort Size;
    public IntPtr Data;
}

internal static class UvcQuery
{
    public const byte SetCur = 0x01;
    public const byte GetCur = 0x81;
    public const byte GetLen = 0x85;
    public const byte GetInfo = 0x86;
}

internal static class V4l2Cid
{
    private const uint UserBase = 0x00980900;
    private const uint CameraBase = 0x009A0900;

    public const uint Brightness = UserBase + 0;
    public const uint Contrast = UserBase + 1;
    public const uint Saturation = UserBase + 2;
    public const uint Hue = UserBase + 3;
    public const uint AutoWhiteBalance = UserBase + 12;
    public const uint Gamma = UserBase + 16;
    public const uint Gain = UserBase + 19;
    public const uint WhiteBalanceTemperature = UserBase + 26;
    public const uint Sharpness = UserBase + 27;
    public const uint BacklightCompensation = UserBase + 28;

    /// <summary>Menu: 1 = manual, 3 = aperture priority (what uvcvideo offers as "auto").</summary>
    public const uint ExposureAuto = CameraBase + 1;
    /// <summary>In 100 µs units (DirectShow reports log2 seconds instead).</summary>
    public const uint ExposureAbsolute = CameraBase + 2;
    /// <summary>Arc seconds (DirectShow reports degrees).</summary>
    public const uint PanAbsolute = CameraBase + 8;
    public const uint TiltAbsolute = CameraBase + 9;
    public const uint FocusAbsolute = CameraBase + 10;
    public const uint FocusAuto = CameraBase + 12;
    public const uint ZoomAbsolute = CameraBase + 13;
    public const uint IrisAbsolute = CameraBase + 17;
    /// <summary>CT_PANTILT_RELATIVE speed; uvcvideo returns the max speed byte unsigned.</summary>
    public const uint PanSpeed = CameraBase + 32;
    public const uint TiltSpeed = CameraBase + 33;

    public const int ExposureManual = 1;
    public const int ExposureAperturePriority = 3;
}

[SupportedOSPlatform("linux")]
internal static unsafe partial class LibC
{
    public const int O_RDWR = 0x2;
    public const int O_NONBLOCK = 0x800;

    // _IOWR(type, nr, size) on x86/ARM: dir 3 (read|write) in the top two bits.
    public static nuint IoWR(char type, int nr, int size) =>
        (nuint)((3u << 30) | ((uint)size << 16) | ((uint)type << 8) | (uint)nr);

    public static readonly nuint VIDIOC_QUERYCTRL = IoWR('V', 36, sizeof(V4l2QueryCtrl));
    public static readonly nuint VIDIOC_G_CTRL = IoWR('V', 27, sizeof(V4l2Control));
    public static readonly nuint VIDIOC_S_CTRL = IoWR('V', 28, sizeof(V4l2Control));
    public static readonly nuint UVCIOC_CTRL_QUERY = IoWR('u', 0x21, Marshal.SizeOf<UvcXuControlQuery>());

    [LibraryImport("libc", EntryPoint = "open", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    public static partial int Open(string path, int flags);

    [LibraryImport("libc", EntryPoint = "close", SetLastError = true)]
    public static partial int Close(int fd);

    [LibraryImport("libc", EntryPoint = "ioctl", SetLastError = true)]
    public static partial int Ioctl(int fd, nuint request, void* arg);

    /// <summary>ioctl that throws <see cref="IOException"/> with the errno text.</summary>
    public static void IoctlOrThrow(int fd, nuint request, void* arg, string what)
    {
        if (Ioctl(fd, request, arg) < 0)
            throw new IOException($"{what}: {Marshal.GetLastPInvokeErrorMessage()} (errno {Marshal.GetLastPInvokeError()})");
    }
}
