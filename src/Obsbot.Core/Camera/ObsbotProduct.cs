namespace Obsbot.Core.Camera;

/// <summary>Version of the 0xAA message protocol a camera speaks ([dev+0x1668] in libdev).</summary>
public enum RemoProtocol
{
    /// <summary>Serializer RVA 0xFFF40: byte 3 = 0x10 | type, nibble-packed ids.</summary>
    V1 = 0,

    /// <summary>Serializer RVA 0x1001B0: different header and id tables (not implemented).</summary>
    V3 = 3,
}

/// <summary>
/// libdev's ObsbotProductType, derived from the USB product id (sub_167F10, stored in
/// [dev+0x680]). Most model checks in libdev and OBSBOT_Main compare this number.
/// </summary>
public static class ObsbotProduct
{
    /// <summary>libdev's value for an unrecognised PID.</summary>
    public const int Unknown = 26;

    private static readonly Dictionary<int, int> TypeByPid = new()
    {
        [0xFEF0] = 0, [0xFEF1] = 7, [0xFEF2] = 5, [0xFEF3] = 6, [0xFEF4] = 1, [0xFEF5] = 26,
        [0xFEF6] = 4, [0xFEF7] = 26, [0xFEF8] = 2, [0xFEF9] = 3, [0xFEFA] = 8, [0xFEFB] = 10,
        [0xFEFC] = 11, [0xFEFD] = 16, [0xFEFE] = 13, [0xFEFF] = 12, [0xFF00] = 26, [0xFF01] = 9,
        [0xFF02] = 18, [0xFF03] = 26, [0xFF04] = 19, [0xFF05] = 26, [0xFF06] = 25, [0xF001] = 22,
        [0xFFFE] = 18,
    };

    /// <summary>Product type for a PID; <see cref="Unknown"/> when the PID is not in libdev's table.</summary>
    public static int TypeFromPid(int? productId) =>
        productId is { } pid && TypeByPid.TryGetValue(pid, out var t) ? t : Unknown;

    /// <summary>
    /// libdev sets [dev+0x1668] = 3 for the types in mask 0x24D3F1C, otherwise 0.
    /// The Tiny (PID FEF0, type 0) speaks V1.
    /// </summary>
    public static RemoProtocol Protocol(int productType) =>
        productType is >= 0 and < 32 && ((0x24D3F1Cu >> productType) & 1) != 0 ? RemoProtocol.V3 : RemoProtocol.V1;
}
