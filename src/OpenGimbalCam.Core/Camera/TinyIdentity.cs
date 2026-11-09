using System.Globalization;
using System.Text.RegularExpressions;

namespace OpenGimbalCam.Core.Camera;

/// <summary>
/// The only device this project operates: the original OBSBOT Tiny, USB\VID_3564&amp;PID_FEF0
/// (ADR-0007). Every enumeration path filters through <see cref="IsMatch"/>.
/// </summary>
public static partial class TinyIdentity
{
    /// <summary>USB vendor id of Remo Tech (OBSBOT).</summary>
    public const int VendorId = 0x3564;

    /// <summary>USB product id of the original OBSBOT Tiny.</summary>
    public const int ProductId = 0xFEF0;

    public const string NotSupportedMessage =
        "dispositivo não suportado: este projeto opera apenas a OBSBOT Tiny original (USB VID_3564&PID_FEF0).";

    public static bool IsMatch(int? vendorId, int? productId) =>
        vendorId == VendorId && productId == ProductId;

    /// <summary>
    /// Extracts VID/PID from a Windows device path ("...vid_3564&amp;pid_fef0..."), case-insensitive.
    /// </summary>
    public static bool TryParseDevicePath(string? path, out int vendorId, out int productId)
    {
        vendorId = productId = 0;
        var m = VidPidRegex().Match(path ?? "");
        if (!m.Success)
            return false;
        vendorId = int.Parse(m.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        productId = int.Parse(m.Groups[2].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return true;
    }

    public static bool IsMatchDevicePath(string? path) =>
        TryParseDevicePath(path, out var v, out var p) && IsMatch(v, p);

    [GeneratedRegex(@"vid_([0-9a-f]{4})&pid_([0-9a-f]{4})", RegexOptions.IgnoreCase)]
    private static partial Regex VidPidRegex();
}
