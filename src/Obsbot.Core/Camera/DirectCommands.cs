namespace Obsbot.Core.Camera;

public enum ArgKind
{
    /// <summary>One byte per argument, in order.</summary>
    Bytes,

    /// <summary>A single little-endian 16-bit value.</summary>
    UInt16,
}

/// <summary>
/// A command for XU selector 6. Wire format (60 bytes, zero padded):
/// <c>[0]=id, [1]=payload length, [2..]=payload</c>.
/// </summary>
/// <param name="Name">CLI name.</param>
/// <param name="Id">Command byte.</param>
/// <param name="Length">Payload length written to byte 1.</param>
/// <param name="Kind">How CLI arguments are packed into the payload.</param>
/// <param name="LibdevFunction">libdev.dll function that builds it (evidence).</param>
/// <param name="CallSite">RVA of the uvcExtSet call inside libdev.dll.</param>
public sealed record DirectCommand(string Name, byte Id, byte Length, ArgKind Kind, string LibdevFunction, uint CallSite)
{
    public int ArgCount => Kind == ArgKind.UInt16 ? 1 : Length;

    public byte[] Build(IReadOnlyList<int> args)
    {
        if (args.Count != ArgCount)
            throw new ArgumentException($"'{Name}' espera {ArgCount} argumento(s), recebeu {args.Count}.");

        var payload = new byte[2 + Length];
        payload[0] = Id;
        payload[1] = Length;
        if (Kind == ArgKind.UInt16)
        {
            int v = CheckRange(args[0], 0, 0xFFFF);
            payload[2] = (byte)v;
            payload[3] = (byte)(v >> 8);
        }
        else
        {
            for (int i = 0; i < Length; i++)
                payload[2 + i] = (byte)CheckRange(args[i], sbyte.MinValue, byte.MaxValue);
        }
        return payload;
    }

    private int CheckRange(int v, int min, int max) =>
        v >= min && v <= max ? v : throw new ArgumentOutOfRangeException(Name, v, $"valor fora de [{min}, {max}]");
}

/// <summary>
/// Direct commands recovered from libdev.dll (OBSBOT Center 2.0.14.64).
/// Only byte layouts are known for sure; the meaning of each value follows
/// the libdev function name and must be confirmed per camera model.
/// </summary>
public static class DirectCommands
{
    public static readonly IReadOnlyList<DirectCommand> All =
    [
        new("media-mode", 0x00, 1, ArgKind.Bytes, "cameraSetMediaModeU", 0x2F397),
        new("hdr", 0x01, 1, ArgKind.Bytes, "cameraSetWdrR", 0x2EE0E),
        new("face-ae", 0x03, 1, ArgKind.Bytes, "cameraSetFaceAER", 0x2E6EB),
        new("fov", 0x04, 1, ArgKind.Bytes, "cameraSetFovU", 0x2ECD9),
        new("bg-mode", 0x05, 1, ArgKind.Bytes, "cameraSetBgModeU", 0x2F449),
        new("mask-level", 0x06, 1, ArgKind.Bytes, "cameraSetMaskLevelU", 0x2F672),
        new("button-mode", 0x07, 1, ArgKind.Bytes, "cameraSetButtonModeU", 0x2FC09),
        new("button-action", 0x08, 3, ArgKind.Bytes, "cameraSetCustomizeButtonActionU", 0x2FCCC),
        new("pan-tilt-relative", 0x09, 4, ArgKind.Bytes, "cameraSetPanTiltRelative", 0x11DD4),
        new("suspend-time", 0x0B, 2, ArgKind.UInt16, "cameraSetSuspendTimeU", 0x2EA24),
        new("vertical-mode", 0x0C, 1, ArgKind.Bytes, "cameraSetVerticalModeU", 0x2EC29),
        new("auto-framing", 0x0D, 2, ArgKind.Bytes, "cameraSetAutoFramingModeU", 0x2EAD2),
        new("resource-action", 0x0E, 2, ArgKind.Bytes, "cameraSetResourceActionU", 0x2EB82),
        new("bg-color", 0x10, 1, ArgKind.Bytes, "cameraSetBgColorU", 0x2F4F9),
        new("bg-enable", 0x11, 1, ArgKind.Bytes, "cameraSetBgEnableU", 0x2F5AC),
        new("no-sleep-without-stream", 0x12, 1, ArgKind.Bytes, "cameraSetDisableSleepWithoutStreamU", 0x2E809),
        new("mic-during-sleep", 0x13, 1, ArgKind.Bytes, "cameraSetMicrophoneDuringSleepU", 0x2E8B9),
        new("flip-horizontal", 0x14, 1, ArgKind.Bytes, "cameraSetImageFlipHorizonU", 0x2E969),
        new("audio-ctrl", 0x15, 2, ArgKind.Bytes, "cameraSetAudioCtrlStateU", 0x2F7D2),
        new("ai-mode", 0x16, 2, ArgKind.Bytes, "cameraSetAiModeU", 0x2F722),
        new("audio-agc", 0x17, 1, ArgKind.Bytes, "cameraSetAudioAutoGainU", 0x2F879),
        new("led", 0x18, 1, ArgKind.Bytes, "cameraSetLedCtrlU", 0x2F929),
        new("boot-mode", 0x19, 1, ArgKind.Bytes, "cameraSetBootModeU", 0x2F9DF),
        new("audio-distance", 0x1B, 1, ArgKind.Bytes, "cameraSetAudioDistanceU", 0xF7FF9),
        new("mic-disabled", 0x1C, 1, ArgKind.Bytes, "cameraSetAudioMicDisabledU", 0xF7F4C),
        new("audio-mode", 0x22, 2, ArgKind.UInt16, "cameraSetAudioModeU", 0x2FA95),
        new("kws-action", 0x23, 2, ArgKind.Bytes, "cameraSetKwsActionU", 0x2FB58),
        new("doa-range", 0x24, 1, ArgKind.Bytes, "cameraSetDoaRange", 0xF844C),
        new("doa-find-back", 0x25, 1, ArgKind.Bytes, "cameraSetDoaFindBack", 0xF839C),
    ];

    public static DirectCommand? Find(string name) =>
        All.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Same encoding as libdev's cameraSetPanTiltRelative: for each axis a
    /// direction byte (1, 0 or -1) followed by the absolute speed.
    /// </summary>
    public static byte[] PanTiltRelative(int panSpeed, int tiltSpeed)
    {
        static (int Dir, int Speed) Axis(int v) => (Math.Sign(v), Math.Min(Math.Abs(v), 0xFF));
        var (pd, ps) = Axis(panSpeed);
        var (td, ts) = Axis(tiltSpeed);
        return Find("pan-tilt-relative")!.Build([pd, ps, td, ts]);
    }

    /// <summary>XU selector 7 payloads (older models). libdev: [0]=action, [1]=preset id.</summary>
    public static byte[] GimbalPreset(GimbalPresetAction action, int presetId) =>
        [(byte)action, checked((byte)presetId)];
}

public enum GimbalPresetAction : byte
{
    /// <summary>cameraDelGimbalPresetU (call site 0x1F555).</summary>
    Delete = 1,

    /// <summary>cameraTrgGimbalPresetU (call site 0x1F926).</summary>
    Trigger = 3,
}
