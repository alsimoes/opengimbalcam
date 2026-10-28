using System.Buffers.Binary;

namespace Obsbot.Core.Protocol;

/// <summary>
/// A 0xAA request with libdev's logical ids, as the R functions fill it on the stack
/// before calling the dispatcher (RVA 0x8160): sender at B+0xA, receiver at B+0xC
/// (always 0x0B), command id at B+0xE, command set at B+0x10, payload at B+0x1C.
/// </summary>
public sealed record RemoRequest(byte Sender, byte CommandSet, ushort CommandId, byte[] Payload, string Function)
{
    /// <summary>The only receiver the R functions use.</summary>
    public const byte Receiver = 0x0B;

    public override string ToString() =>
        $"{Function}: sender={Sender} set={CommandSet} id=0x{CommandId:X2} payload={Convert.ToHexString(Payload)}";
}

/// <summary>
/// Protocol V1 (the Tiny's): libdev's logical ids to wire nibbles, from the std::map
/// tables the serializer at RVA 0xFFF40 reads, and the requests of the R functions
/// that OBSBOT Center's OSC handlers call.
/// </summary>
public static class RemoV1
{
    /// <summary>Device map at RVA 0x217F28 (sender and receiver).</summary>
    private static readonly Dictionary<int, byte> Devices = new()
    {
        [0] = 0, [1] = 1, [2] = 2, [3] = 3, [4] = 4, [5] = 5, [6] = 6,
        [7] = 8, [8] = 11, [9] = 12, [10] = 13, [11] = 14, [12] = 15,
    };

    /// <summary>Command set map at RVA 0x217E28.</summary>
    private static readonly Dictionary<int, byte> Sets = new()
    {
        [0] = 0, [1] = 1, [2] = 2, [3] = 3, [4] = 4, [5] = 8, [6] = 13,
    };

    /// <summary>Per-set command id maps, reached through the map at RVA 0x217EF8.</summary>
    private static readonly Dictionary<int, Dictionary<int, ushort>> Commands = new()
    {
        [1] = new() { [0] = 0x1CD, [1] = 0x3C4, [2] = 0x3C2, [3] = 0x3D2, [4] = 0x1D2 }, // 0x217E58
        [2] = new() { [0] = 0x01, [2] = 0x04, [3] = 0x06, [4] = 0x0A, [5] = 0x22, [6] = 0x23 }, // 0x217F08
        [3] = new() // 0x217ED8
        {
            [0x00] = 0x23, [0x07] = 0x35, [0x08] = 0x36, [0x0A] = 0x3A, [0x0D] = 0x51, [0x0E] = 0x66,
            [0x0F] = 0x67, [0x10] = 0x90, [0x11] = 0x91, [0x13] = 0x92, [0x15] = 0x94, [0x5A] = 0x93, [0x64] = 0xEE,
        },
        [5] = new() // 0x217EE8
        {
            [0] = 0x01, [3] = 0x10, [4] = 0x60, [5] = 0x61, [6] = 0x64, [7] = 0x65, [8] = 0x63,
            [9] = 0x66, [10] = 0x02, [11] = 0x69, [12] = 0x6A, [13] = 0x6B,
        },
    };

    /// <summary>True when the request has a V1 wire id; libdev drops the others on V1 devices.</summary>
    public static bool IsSupported(RemoRequest r) =>
        Sets.ContainsKey(r.CommandSet) && Commands.TryGetValue(r.CommandSet, out var ids) && ids.ContainsKey(r.CommandId)
            && Devices.ContainsKey(r.Sender);

    /// <summary>
    /// Wire frame for a request. The R functions leave the logical type 0 and the dispatcher
    /// passes type r8 ^ 1 = 1, which the serializer turns into wire type 0.
    /// </summary>
    public static RemoMessage ToWire(RemoRequest r, ushort sequence)
    {
        if (!IsSupported(r))
            throw new NotSupportedException($"{r.Function}: set {r.CommandSet} id 0x{r.CommandId:X2} não existe no protocolo V1.");
        return new RemoMessage(RemoPacketType.Type0, sequence, Devices[r.Sender], Devices[RemoRequest.Receiver],
            Sets[r.CommandSet], Commands[r.CommandSet][r.CommandId], r.Payload);
    }

    /// <summary>cameraSetDevRunStatusR (RVA 0x2E040), V1 payload [1, status]. OSC WakeSleep: 0 -> 3 (sleep), other -> 1 (run).</summary>
    public static RemoRequest DevRunStatus(byte status) => new(1, 1, 2, [1, status], "cameraSetDevRunStatusR");

    /// <summary>aiSetTrackingModeR (RVA 0x1AC00): one AiVerticalTrackType byte.</summary>
    public static RemoRequest TrackingMode(byte verticalTrackType) => new(3, 3, 0x11, [verticalTrackType], "aiSetTrackingModeR");

    /// <summary>aiSetTargetSelectR (RVA 0x18E50): lock = id 0x0F with payload [0], unlock = id 0x0E without payload.</summary>
    public static RemoRequest TargetSelect(bool select) =>
        select ? new(3, 3, 0x0F, [0], "aiSetTargetSelectR") : new(3, 3, 0x0E, [], "aiSetTargetSelectR");

    /// <summary>
    /// gimbalSetSpeedPositionR (RVA 0x104480), as sent to the Tiny (id 3 of set 2): six int16 LE
    /// of value * 100, speeds first (roll, pitch, yaw in °/s) then positions (roll, pitch, yaw in °).
    /// </summary>
    public static RemoRequest GimbalSpeedPosition(double roll, double pitch, double yaw, double rollSpeed, double pitchSpeed, double yawSpeed)
    {
        var payload = new byte[12];
        double[] values = [rollSpeed, pitchSpeed, yawSpeed, roll, pitch, yaw];
        for (int i = 0; i < values.Length; i++)
            BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(2 * i), (short)Math.Clamp(Math.Truncate(values[i] * 100), short.MinValue, short.MaxValue));
        return new(2, 2, 3, payload, "gimbalSetSpeedPositionR");
    }
}
