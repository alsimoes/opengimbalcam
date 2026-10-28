using System.Buffers.Binary;

namespace Obsbot.Core.Protocol;

/// <summary>Packet type field (low 2 bits of byte 3).</summary>
public enum RemoPacketType : byte
{
    /// <summary>libdev logical type 1.</summary>
    Type0 = 0,

    /// <summary>libdev logical type 2.</summary>
    Type1 = 1,

    /// <summary>libdev logical type 0 (default for requests).</summary>
    Type2 = 2,
}

/// <summary>
/// Framed message carried over XU selector 2 (max 60 bytes per transfer),
/// as built by libdev.dll serializer RVA 0xFFF40 + finalizer RVA 0xFF7D0:
/// <code>
///  0     0xAA                    start of frame
///  1..2  length (BE, 12 bits)    total length, header included
///  3     0x10 | type             protocol version 1 + packet type
///  4..5  sequence (BE)
///  6..7  CRC-16/USB (BE)         computed with bytes 6..8 = 0
///  8     0x00                    (CRC-8 slot, only for another header variant)
///  9     receiver&lt;&lt;4 | sender
///  10..11 cmdSet&lt;&lt;12 | cmdId (BE)
///  12..  payload
/// </code>
/// All fields here are wire values. libdev translates its logical ids into
/// these nibbles through std::map tables (see docs/investigacao.md, 6.3).
/// </summary>
public sealed record RemoMessage(
    RemoPacketType Type,
    ushort Sequence,
    byte Sender,
    byte Receiver,
    byte CommandSet,
    ushort CommandId,
    byte[] Payload)
{
    public const byte StartOfFrame = 0xAA;
    public const int HeaderLength = 12;
    public const int MaxLength = 60;

    public byte[] Encode()
    {
        if (Sender > 0xF || Receiver > 0xF || CommandSet > 0xF || CommandId > 0xFFF)
            throw new ArgumentOutOfRangeException(nameof(RemoMessage), "Campo excede o tamanho do nibble/12 bits.");
        int length = HeaderLength + Payload.Length;
        if (length > MaxLength)
            throw new ArgumentException($"Mensagem de {length} bytes excede o limite de {MaxLength} da XU.");

        var buf = new byte[length];
        buf[0] = StartOfFrame;
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(1), (ushort)length);
        buf[3] = (byte)(0x10 | (byte)Type);
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(4), Sequence);
        buf[9] = (byte)((Receiver << 4) | Sender);
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(10), (ushort)((CommandSet << 12) | CommandId));
        Payload.CopyTo(buf, HeaderLength);
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(6), Crc16Usb.Compute(buf));
        return buf;
    }

    /// <summary>Parses a frame; returns null when the buffer does not hold a valid one.</summary>
    public static RemoMessage? TryDecode(ReadOnlySpan<byte> data, out string? error)
    {
        error = null;
        if (data.Length < HeaderLength || data[0] != StartOfFrame)
        {
            error = "sem cabeçalho 0xAA";
            return null;
        }
        int length = BinaryPrimitives.ReadUInt16BigEndian(data[1..]) & 0xFFF;
        if (length < HeaderLength || length > data.Length)
        {
            error = $"comprimento inválido ({length})";
            return null;
        }

        var frame = data[..length].ToArray();
        ushort crc = BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(6));
        frame[6] = frame[7] = frame[8] = 0;
        if (Crc16Usb.Compute(frame) != crc)
        {
            error = "CRC inválido";
            return null;
        }

        ushort cmd = BinaryPrimitives.ReadUInt16BigEndian(data[10..]);
        return new RemoMessage(
            (RemoPacketType)(data[3] & 0x3),
            BinaryPrimitives.ReadUInt16BigEndian(data[4..]),
            (byte)(data[9] & 0xF),
            (byte)(data[9] >> 4),
            (byte)(cmd >> 12),
            (ushort)(cmd & 0xFFF),
            data[HeaderLength..length].ToArray());
    }

    public override string ToString() =>
        $"type={Type} seq={Sequence} sender={Sender} receiver={Receiver} set={CommandSet} id=0x{CommandId:X3} payload={Convert.ToHexString(Payload)}";
}
