using Obsbot.Core.Protocol;

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
    BacklightCompensation,
    Gain,
    PowerlineFrequency,
}

/// <summary>
/// The operations the OSC bridge needs from one camera. Implemented over the
/// OBSBOT Extension Unit and the standard UVC controls by <see cref="ObsbotCamera"/>,
/// and by a fake in the tests.
/// </summary>
public interface ICamera : IDisposable
{
    string Name { get; }

    /// <summary>
    /// libdev's ObsbotProductType, sent as the last DeviceInfo argument (0 = Tiny;
    /// the TouchOSC samples open the Tiny page for it).
    /// </summary>
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

    /// <summary>Sets a control to a raw UVC value (e.g. white balance in kelvin), clamped to its range.</summary>
    void SetRaw(UvcControl control, int value);

    /// <summary>Whether the control is in automatic mode; null when the camera does not have it.</summary>
    bool? IsAuto(UvcControl control);

    /// <summary>Manual exposure of 1/<paramref name="denominator"/> s, in the platform's UVC units.</summary>
    void SetShutterSpeed(int denominator);

    /// <summary>A direct command (XU selector 6) by its <see cref="Obsbot.Core.Camera.DirectCommands"/> name.</summary>
    void SendCommand(string name, params int[] args);

    /// <summary>A 0xAA message on XU selector 2, in the Tiny's protocol V1.</summary>
    void SendMessage(RemoRequest request);
}

/// <summary>The cameras the bridge can address. OSC device indexes 0..3 index into this list.</summary>
public interface ICameraHub
{
    IReadOnlyList<ICamera> Cameras { get; }

    /// <summary>Re-enumerates devices (hot plug). Existing sessions may be disposed.</summary>
    void Refresh();
}

/// <summary>
/// Video on the PC (OBSBOT Center does this in its media process, OBSBOT_Center.exe).
/// Provided by the GUI, which has the camera's video open.
/// </summary>
public interface IPcMedia
{
    /// <summary>OSC PCSnapshot: saves the current frame of device <paramref name="device"/>.</summary>
    void Snapshot(int device);

    /// <summary>OSC SetPCRecording / SetHighfpsRecording.</summary>
    void SetRecording(int device, bool on, bool highFps);
}
