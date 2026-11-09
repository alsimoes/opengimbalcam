using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace OpenGimbalCam.Osc;

/// <summary>
/// An OSC 1.0 message. Supports the argument types OBSBOT Center's liblo server
/// registers (i, f, s) plus the common extras (b, h, d, T, F, N).
/// </summary>
public sealed record OscMessage(string Address, IReadOnlyList<object?> Args)
{
    public OscMessage(string address, params object?[] args) : this(address, (IReadOnlyList<object?>)args)
    {
    }

    /// <summary>The OSC type tag string without the leading comma, e.g. "isi".</summary>
    public string TypeTags => string.Concat(Args.Select(TagOf));

    public int Int(int index) => Args[index] switch
    {
        int i => i,
        long l => checked((int)l),
        float f => (int)f,
        double d => (int)d,
        bool b => b ? 1 : 0,
        var other => throw new FormatException($"argumento {index} de {Address} não é numérico: {other}"),
    };

    public byte[] Encode()
    {
        using var ms = new MemoryStream();
        WriteString(ms, Address);
        WriteString(ms, "," + TypeTags);
        foreach (var a in Args)
        {
            switch (a)
            {
                case int i: WriteBE(ms, (uint)i); break;
                case float f: WriteBE(ms, BitConverter.SingleToUInt32Bits(f)); break;
                case string s: WriteString(ms, s); break;
                case byte[] blob:
                    WriteBE(ms, (uint)blob.Length);
                    ms.Write(blob);
                    Pad(ms, blob.Length);
                    break;
                case long l: WriteBE(ms, (ulong)l); break;
                case double d: WriteBE(ms, BitConverter.DoubleToUInt64Bits(d)); break;
                case bool or null: break;
                default: throw new ArgumentException($"tipo OSC não suportado: {a.GetType().Name}");
            }
        }
        return ms.ToArray();
    }

    public override string ToString() =>
        Args.Count == 0 ? Address : $"{Address} {string.Join(' ', Args.Select(Format))}";

    private static string Format(object? a) => a switch
    {
        string s => $"\"{s}\"",
        float f => f.ToString("0.###", CultureInfo.InvariantCulture) + "f",
        double d => d.ToString("0.###", CultureInfo.InvariantCulture) + "d",
        byte[] b => $"<blob {b.Length}>",
        null => "nil",
        bool b => b ? "true" : "false",
        _ => Convert.ToString(a, CultureInfo.InvariantCulture) ?? "",
    };

    private static char TagOf(object? a) => a switch
    {
        int => 'i',
        float => 'f',
        string => 's',
        byte[] => 'b',
        long => 'h',
        double => 'd',
        true => 'T',
        false => 'F',
        null => 'N',
        _ => throw new ArgumentException($"tipo OSC não suportado: {a.GetType().Name}"),
    };

    /// <summary>
    /// Decodes an OSC packet: a message, or a bundle (flattened, time tags ignored,
    /// as liblo dispatches bundle contents immediately when the time tag is "now").
    /// </summary>
    public static IReadOnlyList<OscMessage> DecodePacket(ReadOnlySpan<byte> data)
    {
        var list = new List<OscMessage>();
        DecodeInto(data, list, depth: 0);
        return list;
    }

    private static void DecodeInto(ReadOnlySpan<byte> data, List<OscMessage> list, int depth)
    {
        if (data.Length == 0)
            throw new FormatException("pacote OSC vazio");
        if (data[0] == (byte)'#')
        {
            if (depth > 8)
                throw new FormatException("bundles OSC aninhados demais");
            int pos = 0;
            if (ReadString(data, ref pos) != "#bundle")
                throw new FormatException("pacote começa com '#' mas não é #bundle");
            pos += 8; // time tag
            while (pos < data.Length)
            {
                int size = checked((int)ReadUInt32(data, ref pos));
                if (size < 0 || pos + size > data.Length)
                    throw new FormatException("elemento de bundle truncado");
                DecodeInto(data.Slice(pos, size), list, depth + 1);
                pos += size;
            }
            return;
        }
        list.Add(Decode(data));
    }

    public static OscMessage Decode(ReadOnlySpan<byte> data)
    {
        int pos = 0;
        var address = ReadString(data, ref pos);
        if (!address.StartsWith('/'))
            throw new FormatException($"endereço OSC inválido: '{address}'");
        if (pos >= data.Length)
            return new OscMessage(address, Array.Empty<object?>()); // OSC 1.0 allows a missing type tag
        var tags = ReadString(data, ref pos);
        if (!tags.StartsWith(','))
            throw new FormatException("type tag OSC sem vírgula inicial");
        var args = new List<object?>();
        foreach (char t in tags.AsSpan(1))
        {
            args.Add(t switch
            {
                'i' => (int)ReadUInt32(data, ref pos),
                'f' => BitConverter.UInt32BitsToSingle(ReadUInt32(data, ref pos)),
                's' or 'S' => ReadString(data, ref pos),
                'b' => ReadBlob(data, ref pos),
                'h' => (long)ReadUInt64(data, ref pos),
                'd' => BitConverter.UInt64BitsToDouble(ReadUInt64(data, ref pos)),
                'T' => true,
                'F' => false,
                'N' or 'I' => null,
                _ => throw new FormatException($"tipo OSC '{t}' não suportado"),
            });
        }
        return new OscMessage(address, args);
    }

    private static string ReadString(ReadOnlySpan<byte> data, ref int pos)
    {
        int end = data[pos..].IndexOf((byte)0);
        if (end < 0)
            throw new FormatException("string OSC sem terminador");
        var s = Encoding.UTF8.GetString(data.Slice(pos, end));
        pos += (end + 4) & ~3;
        if (pos > data.Length)
            throw new FormatException("string OSC truncada");
        return s;
    }

    private static byte[] ReadBlob(ReadOnlySpan<byte> data, ref int pos)
    {
        int len = checked((int)ReadUInt32(data, ref pos));
        if (len < 0 || pos + len > data.Length)
            throw new FormatException("blob OSC truncado");
        var blob = data.Slice(pos, len).ToArray();
        pos += (len + 3) & ~3;
        return blob;
    }

    private static uint ReadUInt32(ReadOnlySpan<byte> data, ref int pos)
    {
        if (pos + 4 > data.Length)
            throw new FormatException("argumento OSC truncado");
        var v = BinaryPrimitives.ReadUInt32BigEndian(data[pos..]);
        pos += 4;
        return v;
    }

    private static ulong ReadUInt64(ReadOnlySpan<byte> data, ref int pos)
    {
        if (pos + 8 > data.Length)
            throw new FormatException("argumento OSC truncado");
        var v = BinaryPrimitives.ReadUInt64BigEndian(data[pos..]);
        pos += 8;
        return v;
    }

    private static void WriteString(Stream s, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        s.Write(bytes);
        s.WriteByte(0);
        Pad(s, bytes.Length + 1);
    }

    private static void Pad(Stream s, int written)
    {
        for (int i = written; (i & 3) != 0; i++)
            s.WriteByte(0);
    }

    private static void WriteBE(Stream s, uint v)
    {
        Span<byte> b = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(b, v);
        s.Write(b);
    }

    private static void WriteBE(Stream s, ulong v)
    {
        Span<byte> b = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(b, v);
        s.Write(b);
    }
}
