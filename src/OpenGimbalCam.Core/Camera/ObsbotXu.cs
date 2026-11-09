namespace OpenGimbalCam.Core.Camera;

/// <summary>
/// Constants of the OBSBOT UVC Extension Unit, recovered from libdev.dll
/// (OBSBOT Center 2.0.14.64). See .docs/investigacao.md, section 5.
/// </summary>
public static class ObsbotXu
{
    /// <summary>XU GUID (property set) used in every KSP_NODE built by libdev's uvcExtGet/uvcExtSet.</summary>
    public static readonly Guid Guid = new("9A1E7291-6843-4683-6D92-39BC7906EE49");

    /// <summary>libdev always transfers exactly 60 bytes (0x3C) per XU control.</summary>
    public const int PayloadLength = 60;

    /// <summary>Control selectors (KSP_NODE.Property.Id) and what libdev uses them for.</summary>
    public enum Selector : uint
    {
        /// <summary>transFileStart: start of a file upload to the camera.</summary>
        FileTransferStart = 1,

        /// <summary>Framed 0xAA message channel (sendMsg / rxMsg).</summary>
        Message = 2,

        /// <summary>ugSyscall: runs a shell command on the camera's Linux (reboot, dd, bspatch...).</summary>
        Syscall = 3,

        /// <summary>fileDownloadReq / REMO_UVC_XU_CTRL_FILE_DOWNLOAD.</summary>
        FileDownload = 4,

        /// <summary>ugUpCmd: firmware upgrade state machine (PACK_LOG, BSPATCH, FREE_SOURCE...).</summary>
        Upgrade = 5,

        /// <summary>Direct camera commands: [cmd][len][payload] (cameraSet*U functions).</summary>
        Command = 6,

        /// <summary>Gimbal presets on older models (cameraAdd/Del/TrgGimbalPresetU).</summary>
        GimbalPreset = 7,
    }

    /// <summary>
    /// Selectors that can change firmware/flash or execute code on the device.
    /// The CLI refuses them unless explicitly forced.
    /// </summary>
    public static bool IsDangerous(uint selector) =>
        selector is (uint)Selector.FileTransferStart or (uint)Selector.Syscall
            or (uint)Selector.FileDownload or (uint)Selector.Upgrade;
}
