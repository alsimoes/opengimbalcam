namespace Obsbot.Osc.Bridge;

/// <summary>UVC controls that OBSBOT Center's OSC protocol can change on webcams.</summary>
public enum UvcControl
{
    Focus,
    Exposure,
    WhiteBalance,
    Contrast,
    Saturation,
    Sharpness,
    Hue,
}

/// <summary>
/// The operations the OSC bridge needs from one camera. Implemented over the
/// OBSBOT Extension Unit and the standard UVC controls by <see cref="ObsbotCamera"/>,
/// and by a fake in the tests.
/// </summary>
public interface ICamera : IDisposable
{
    string Name { get; }

    /// <summary>Product code sent as the last DeviceInfo argument (see docs §10.4).</summary>
    int ProductCode { get; }

    /// <summary>libdev cameraSetPanTiltRelative(pan, tilt): speeds in [-1, 1], 0 stops.</summary>
    void SetPanTiltSpeed(double pan, double tilt);

    /// <summary>Gimbal to the home position: cameraSetPanTiltAbsolute(0, 0).</summary>
    void ResetPanTilt();

    /// <summary>Pan/tilt in degrees (UVC absolute values on OBSBOT cameras).</summary>
    (int Pan, int Tilt)? GetPanTilt();

    /// <summary>Zoom as 0..100 % of the UVC range (OSC SetZoom/ZoomInfo scale).</summary>
    int? GetZoomPercent();

    void SetZoomPercent(int percent);

    /// <summary>libdev cameraSetFovU: 0 = 86°, 1 = 78°, 2 = 65°.</summary>
    void SetFov(int fovType);

    /// <summary>libdev cameraSetImageFlipHorizonU.</summary>
    void SetMirror(bool on);

    /// <summary>libdev cameraTrgGimbalPresetU (XU selector 7).</summary>
    void TriggerPreset(int presetId);

    void SetAuto(UvcControl control, bool auto);

    /// <summary>Sets a control to a 0..100 % position of its UVC range.</summary>
    void SetPercent(UvcControl control, int percent);

    /// <summary>Sets a control to a raw UVC value (e.g. white balance in kelvin).</summary>
    void SetRaw(UvcControl control, int value);
}

/// <summary>The cameras the bridge can address. OSC device indexes 0..3 index into this list.</summary>
public interface ICameraHub
{
    IReadOnlyList<ICamera> Cameras { get; }

    /// <summary>Re-enumerates devices (hot plug). Existing sessions may be disposed.</summary>
    void Refresh();
}
