namespace OpenGimbalCam.Devices.Linux;

/// <summary>A unit or terminal of a UVC VideoControl interface.</summary>
/// <param name="Subtype">VC descriptor subtype: 2 input terminal, 3 output terminal, 4 selector,
/// 5 processing unit, 6 extension unit, 7 encoding unit.</param>
/// <param name="Guid">guidExtensionCode, for extension units only.</param>
public readonly record struct UvcUnit(byte Id, byte Subtype, Guid? Guid)
{
    public string Kind => Subtype switch
    {
        2 => "Input Terminal",
        3 => "Output Terminal",
        4 => "Selector Unit",
        5 => "Processing Unit",
        6 => "Extension Unit",
        7 => "Encoding Unit",
        _ => $"subtipo 0x{Subtype:X2}",
    };
}

/// <summary>
/// Parses the raw USB descriptors that Linux exposes in
/// /sys/bus/usb/devices/&lt;dev&gt;/descriptors (device + configuration descriptors,
/// back to back). uvcvideo addresses an XU by its bUnitID, which only the descriptors
/// tell; Windows hides it behind a KS node id instead.
/// </summary>
public static class UvcDescriptors
{
    private const byte Interface = 0x04;
    private const byte CsInterface = 0x24;
    private const byte VideoClass = 0x0E;
    private const byte VideoControlSubclass = 0x01;
    private const byte VcHeader = 0x01;
    private const byte VcExtensionUnit = 0x06;

    public static IReadOnlyList<UvcUnit> ParseUnits(ReadOnlySpan<byte> descriptors)
    {
        var units = new List<UvcUnit>();
        bool inVideoControl = false;
        for (int i = 0; i + 2 <= descriptors.Length;)
        {
            int length = descriptors[i];
            if (length < 2 || i + length > descriptors.Length)
                break;
            var d = descriptors.Slice(i, length);
            i += length;

            if (d[1] == Interface && length >= 9)
            {
                // Class-specific VS descriptors reuse subtype 0x06 (VS_FORMAT_MJPEG),
                // so only VideoControl interfaces are considered.
                inVideoControl = d[5] == VideoClass && d[6] == VideoControlSubclass;
                continue;
            }
            if (!inVideoControl || d[1] != CsInterface || length < 4 || d[2] == VcHeader)
                continue;

            byte subtype = d[2];
            Guid? guid = subtype == VcExtensionUnit && length >= 20 ? new Guid(d.Slice(4, 16)) : null;
            units.Add(new UvcUnit(d[3], subtype, guid));
        }
        return units;
    }

    /// <summary>bUnitID of the extension unit with the given GUID, or null.</summary>
    public static byte? FindExtensionUnit(ReadOnlySpan<byte> descriptors, Guid guid)
    {
        foreach (var u in ParseUnits(descriptors))
        {
            if (u.Guid == guid)
                return u.Id;
        }
        return null;
    }
}
