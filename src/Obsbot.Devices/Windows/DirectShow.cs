using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using Obsbot.Core.Camera;

namespace Obsbot.Devices.Windows;

// Minimal DirectShow surface used by OBSBOT's libdev.dll to reach the camera.
// libdev references CLSID_SystemDeviceEnum, CLSID_VideoInputDeviceCategory,
// IAMCameraControl and IAMVideoProcAmp (see docs/investigacao.md, section 4).

internal static class DsGuids
{
    public static readonly Guid CLSID_SystemDeviceEnum = new("62BE5D10-60EB-11D0-BD3B-00A0C911CE86");
    public static readonly Guid CLSID_VideoInputDeviceCategory = new("860BB310-5D01-11D0-BD3B-00A0C911CE86");
    public static readonly Guid IID_IBaseFilter = new("56A86895-0AD4-11CE-B03A-0020AF0BA770");
    public static readonly Guid IID_IPropertyBag = new("55272A00-42CB-11CE-8135-00AA004BB851");
}

[ComImport, Guid("29840822-5B84-11D0-BD3B-00A0C911CE86"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ICreateDevEnum
{
    [PreserveSig]
    int CreateClassEnumerator([In] ref Guid clsidDeviceClass, out IEnumMoniker? enumMoniker, int flags);
}

[ComImport, Guid("55272A00-42CB-11CE-8135-00AA004BB851"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPropertyBag
{
    [PreserveSig]
    int Read([MarshalAs(UnmanagedType.LPWStr)] string propName, out object? value, IntPtr errorLog);

    [PreserveSig]
    int Write([MarshalAs(UnmanagedType.LPWStr)] string propName, ref object value);
}

internal enum CameraControlProperty
{
    Pan = 0,
    Tilt = 1,
    Roll = 2,
    Zoom = 3,
    Exposure = 4,
    Iris = 5,
    Focus = 6,

    // KSPROPERTY_CAMERACONTROL_*_RELATIVE: not in the DirectShow enum, but ksproxy forwards
    // them to usbvideo.sys. libdev reads these ranges to scale cameraSetPanTiltRelative.
    PanRelative = 10,
    TiltRelative = 11,
}

internal enum VideoProcAmpProperty
{
    Brightness = 0,
    Contrast = 1,
    Hue = 2,
    Saturation = 3,
    Sharpness = 4,
    Gamma = 5,
    ColorEnable = 6,
    WhiteBalance = 7,
    BacklightCompensation = 8,
    Gain = 9,
}

[ComImport, Guid("C6E13370-30AC-11D0-A18C-00A0C9118956"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAMCameraControl
{
    [PreserveSig]
    int GetRange(CameraControlProperty property, out int min, out int max, out int step, out int defaultValue, out ControlFlags capsFlags);

    [PreserveSig]
    int Set(CameraControlProperty property, int value, ControlFlags flags);

    [PreserveSig]
    int Get(CameraControlProperty property, out int value, out ControlFlags flags);
}

[ComImport, Guid("C6E13360-30AC-11D0-A18C-00A0C9118956"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAMVideoProcAmp
{
    [PreserveSig]
    int GetRange(VideoProcAmpProperty property, out int min, out int max, out int step, out int defaultValue, out ControlFlags capsFlags);

    [PreserveSig]
    int Set(VideoProcAmpProperty property, int value, ControlFlags flags);

    [PreserveSig]
    int Get(VideoProcAmpProperty property, out int value, out ControlFlags flags);
}
