namespace OpenGimbalCam.Core.Firmware;

/// <summary>Standard CRC-32 (IEEE 802.3, reflected 0xEDB88320), incremental.</summary>
internal sealed class Crc32
{
    private static readonly uint[] Table = BuildTable();
    private uint _state = 0xFFFFFFFF;

    public void Append(ReadOnlySpan<byte> data)
    {
        uint c = _state;
        foreach (var b in data)
            c = Table[(c ^ b) & 0xFF] ^ (c >> 8);
        _state = c;
    }

    public uint GetCurrentHashAsUInt32() => ~_state;

    public static uint HashToUInt32(ReadOnlySpan<byte> data)
    {
        var crc = new Crc32();
        crc.Append(data);
        return crc.GetCurrentHashAsUInt32();
    }

    private static uint[] BuildTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint c = i;
            for (int k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            table[i] = c;
        }
        return table;
    }
}
