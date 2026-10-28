using System.Buffers.Binary;

namespace Obsbot.Osc;

public enum OscFraming
{
    /// <summary>OSC 1.0 over TCP: each packet preceded by its int32 big-endian size (liblo's default).</summary>
    LengthPrefixed,

    /// <summary>OSC 1.1 over TCP: SLIP (RFC 1055) with END (0xC0) delimiters.</summary>
    Slip,
}

/// <summary>
/// Packet framing for OSC over a byte stream. Like liblo, the framing is detected
/// from the first byte the client sends (0xC0 means SLIP) and replies use the same one.
/// </summary>
public sealed class OscStreamFraming
{
    private const byte End = 0xC0, Esc = 0xDB, EscEnd = 0xDC, EscEsc = 0xDD;
    public const int MaxPacket = 64 * 1024;

    private readonly Stream _stream;
    private readonly byte[] _one = new byte[1];

    public OscStreamFraming(Stream stream, OscFraming? framing = null)
    {
        _stream = stream;
        Framing = framing;
    }

    /// <summary>Null until the first byte has been read (or set explicitly for clients).</summary>
    public OscFraming? Framing { get; private set; }

    /// <summary>Returns the next packet, or null at end of stream.</summary>
    public async Task<byte[]?> ReadPacketAsync(CancellationToken ct)
    {
        if (Framing is null)
        {
            if (await _stream.ReadAsync(_one, ct) == 0)
                return null;
            Framing = _one[0] == End ? OscFraming.Slip : OscFraming.LengthPrefixed;
            return Framing == OscFraming.Slip
                ? await ReadSlipAsync(ct)
                : await ReadPrefixedAsync(_one[0], ct);
        }
        return Framing == OscFraming.Slip ? await ReadSlipAsync(ct) : await ReadPrefixedAsync(null, ct);
    }

    public async Task WritePacketAsync(byte[] packet, CancellationToken ct)
    {
        var framed = Frame(packet, Framing ?? OscFraming.LengthPrefixed);
        await _stream.WriteAsync(framed, ct);
        await _stream.FlushAsync(ct);
    }

    public static byte[] Frame(ReadOnlySpan<byte> packet, OscFraming framing)
    {
        if (framing == OscFraming.LengthPrefixed)
        {
            var buf = new byte[4 + packet.Length];
            BinaryPrimitives.WriteInt32BigEndian(buf, packet.Length);
            packet.CopyTo(buf.AsSpan(4));
            return buf;
        }

        var ms = new MemoryStream(packet.Length + 8);
        ms.WriteByte(End);
        foreach (byte b in packet)
        {
            switch (b)
            {
                case End: ms.WriteByte(Esc); ms.WriteByte(EscEnd); break;
                case Esc: ms.WriteByte(Esc); ms.WriteByte(EscEsc); break;
                default: ms.WriteByte(b); break;
            }
        }
        ms.WriteByte(End);
        return ms.ToArray();
    }

    private async Task<byte[]?> ReadPrefixedAsync(byte? first, CancellationToken ct)
    {
        var header = new byte[4];
        int got = 0;
        if (first is { } f)
            header[got++] = f;
        if (!await ReadExactAsync(header.AsMemory(got), ct))
            return null;
        int size = BinaryPrimitives.ReadInt32BigEndian(header);
        if (size <= 0 || size > MaxPacket)
            throw new InvalidDataException($"tamanho de pacote OSC inválido: {size}");
        var packet = new byte[size];
        return await ReadExactAsync(packet, ct) ? packet : null;
    }

    private async Task<byte[]?> ReadSlipAsync(CancellationToken ct)
    {
        var ms = new MemoryStream();
        bool escaped = false;
        while (true)
        {
            if (await _stream.ReadAsync(_one, ct) == 0)
                return null;
            byte b = _one[0];
            if (escaped)
            {
                ms.WriteByte(b switch
                {
                    EscEnd => End,
                    EscEsc => Esc,
                    _ => throw new InvalidDataException($"escape SLIP inválido: 0x{b:X2}"),
                });
                escaped = false;
            }
            else if (b == Esc)
                escaped = true;
            else if (b == End)
            {
                if (ms.Length > 0) // a leading END (or two in a row) delimits an empty packet
                    return ms.ToArray();
            }
            else
                ms.WriteByte(b);

            if (ms.Length > MaxPacket)
                throw new InvalidDataException("pacote OSC/SLIP grande demais");
        }
    }

    private async Task<bool> ReadExactAsync(Memory<byte> buffer, CancellationToken ct)
    {
        int read = 0;
        while (read < buffer.Length)
        {
            int n = await _stream.ReadAsync(buffer[read..], ct);
            if (n == 0)
                return false;
            read += n;
        }
        return true;
    }
}
