namespace Obsbot.Core.Protocol;

/// <summary>
/// CRC-16/USB (poly 0x8005 reflected = 0xA001, init 0xFFFF, xorout 0xFFFF).
/// libdev.dll (RVA 0xFF7D0) uses the classic Modbus hi/lo tables and then
/// inverts the result, which is exactly CRC-16/USB (check value 0xB4C8).
/// </summary>
public static class Crc16Usb
{
    public static ushort Compute(ReadOnlySpan<byte> data)
    {
        ushort crc = 0xFFFF;
        foreach (var b in data)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
                crc = (crc & 1) != 0 ? (ushort)((crc >> 1) ^ 0xA001) : (ushort)(crc >> 1);
        }
        return (ushort)~crc;
    }
}
