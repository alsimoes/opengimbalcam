using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace OpenGimbalCam.Core.Firmware;

/// <summary>
/// One entry of a firmware table. The same 0x100-byte layout is used by the
/// outer OBSBOT package and by the inner "AMAC" container:
/// <code>
///  +0x00 type      +0x04 size      +0x08 offset
///  +0x1C name (AMAC entries)       +0x3C version (4 bytes, printed MSB first)
///  +0x44 CRC-32 of the data (outer package only)
///  +0x48 MD5 of the data (outer) / +0x44 MD5 (AMAC)
///  +0x58 name (outer entries)      +0xFC CRC-32 of bytes 0x00..0xFB of the entry
/// </code>
/// </summary>
public sealed record FirmwareEntry(
    int Index,
    uint Type,
    string Name,
    long Offset,
    long Size,
    string Version,
    uint? ExpectedCrc32,
    byte[] ExpectedMd5,
    bool EntryCrcOk,
    long BaseOffset)
{
    public long AbsoluteOffset => BaseOffset + Offset;
}

public sealed record VerifyResult(FirmwareEntry Entry, bool Md5Ok, bool? Crc32Ok);

/// <summary>
/// Parser for OBSBOT firmware packages such as
/// <c>Obsbot_tiny_2P_OA_E_All_1.2.6.2_release.bin</c>.
/// Layout recovered and validated against that file (.docs/investigacao.md, 7).
/// </summary>
public sealed class FirmwarePackage
{
    public const uint Magic = 0xAB20EC98; // bytes 98 EC 20 AB
    public const uint AmacMagic = 0x43414D41; // "AMAC"

    private readonly string _path;

    private FirmwarePackage(string path) => _path = path;

    public string Product { get; private set; } = "";
    public string Region { get; private set; } = "";
    public string Version { get; private set; } = "";
    public string Chip { get; private set; } = "";
    public string Build { get; private set; } = "";
    public long HeaderLength { get; private set; }
    public long TotalLength { get; private set; }
    public byte[] PayloadMd5 { get; private set; } = [];
    public bool HeaderCrcOk { get; private set; }
    public IReadOnlyList<FirmwareEntry> Components { get; private set; } = [];

    /// <summary>Partitions of the inner AMAC container (SigmaStar Linux image), if present.</summary>
    public IReadOnlyList<FirmwareEntry> Partitions { get; private set; } = [];

    public static FirmwarePackage Open(string path)
    {
        var pkg = new FirmwarePackage(path);
        using var fs = File.OpenRead(path);
        var header = ReadAt(fs, 0, 0x400);
        if (BinaryPrimitives.ReadUInt32LittleEndian(header) != Magic)
            throw new InvalidDataException("Não é um pacote de firmware OBSBOT (magic 98 EC 20 AB ausente).");

        pkg.HeaderLength = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(4));
        pkg.TotalLength = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(8));
        int count = (int)BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(12));
        pkg.Product = CString(header, 0x10, 0x10);
        pkg.Region = CString(header, 0x20, 0x10);
        pkg.Version = string.Join('.', header.AsSpan(0x30, 4).ToArray().Reverse());
        pkg.PayloadMd5 = header.AsSpan(0x38, 16).ToArray();
        pkg.Chip = CString(header, 0x48, 0x10);
        pkg.Build = CString(header, 0x58, 0x10);
        pkg.HeaderCrcOk = Crc32.HashToUInt32(header.AsSpan(0, 0xFC)) == BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(0xFC));

        var components = new List<FirmwareEntry>();
        for (int i = 0; i < count && 0x100 + (i + 1) * 0x100 <= pkg.HeaderLength; i++)
        {
            var e = header.AsSpan(0x100 + i * 0x100, 0x100);
            components.Add(new FirmwareEntry(
                i,
                BinaryPrimitives.ReadUInt32LittleEndian(e),
                CString(e, 0x58, 0xA4),
                BinaryPrimitives.ReadUInt32LittleEndian(e[8..]),
                BinaryPrimitives.ReadUInt32LittleEndian(e[4..]),
                VersionString(e[0x3C..0x40]),
                BinaryPrimitives.ReadUInt32LittleEndian(e[0x44..]),
                e.Slice(0x48, 16).ToArray(),
                EntryCrc(e),
                0));
        }
        pkg.Components = components;

        var amacOwner = components.FirstOrDefault(c => ReadUInt32(fs, c.AbsoluteOffset) == AmacMagic);
        if (amacOwner is not null)
            pkg.Partitions = ReadAmac(fs, amacOwner.AbsoluteOffset);
        return pkg;
    }

    public IEnumerable<VerifyResult> Verify()
    {
        using var fs = File.OpenRead(_path);
        foreach (var e in Components.Concat(Partitions))
        {
            var (md5, crc) = Hash(fs, e.AbsoluteOffset, e.Size);
            yield return new VerifyResult(e, md5.AsSpan().SequenceEqual(e.ExpectedMd5),
                e.ExpectedCrc32 is { } expected ? crc == expected : null);
        }
    }

    public bool VerifyPayloadMd5()
    {
        using var fs = File.OpenRead(_path);
        var (md5, _) = Hash(fs, HeaderLength, fs.Length - HeaderLength);
        return md5.AsSpan().SequenceEqual(PayloadMd5);
    }

    /// <summary>Writes every component and AMAC partition to <paramref name="outputDir"/>.</summary>
    public IReadOnlyList<string> Extract(string outputDir)
    {
        Directory.CreateDirectory(outputDir);
        var written = new List<string>();
        using var fs = File.OpenRead(_path);
        foreach (var (e, prefix) in Components.Select(c => (c, "component")).Concat(Partitions.Select(p => (p, "partition"))))
        {
            var name = $"{prefix}{e.Index}_{Sanitize(e.Name)}";
            var target = Path.Combine(outputDir, name);
            using var outFs = File.Create(target);
            Copy(fs, outFs, e.AbsoluteOffset, e.Size);
            written.Add(target);
        }
        return written;
    }

    private static List<FirmwareEntry> ReadAmac(FileStream fs, long baseOffset)
    {
        var head = ReadAt(fs, baseOffset, 0x10);
        long headerLength = BinaryPrimitives.ReadUInt32LittleEndian(head.AsSpan(4));
        int count = (int)BinaryPrimitives.ReadUInt32LittleEndian(head.AsSpan(12));
        var header = ReadAt(fs, baseOffset, (int)headerLength);
        var list = new List<FirmwareEntry>();
        for (int i = 0; i < count && 0x100 + (i + 1) * 0x100 <= headerLength; i++)
        {
            var e = header.AsSpan(0x100 + i * 0x100, 0x100);
            list.Add(new FirmwareEntry(
                i,
                BinaryPrimitives.ReadUInt32LittleEndian(e),
                CString(e, 0x1C, 0x20),
                BinaryPrimitives.ReadUInt32LittleEndian(e[8..]),
                BinaryPrimitives.ReadUInt32LittleEndian(e[4..]),
                VersionString(e[0x3C..0x40]),
                null,
                e.Slice(0x44, 16).ToArray(),
                EntryCrc(e),
                baseOffset));
        }
        return list;
    }

    private static bool EntryCrc(ReadOnlySpan<byte> entry) =>
        Crc32.HashToUInt32(entry[..0xFC]) == BinaryPrimitives.ReadUInt32LittleEndian(entry[0xFC..]);

    private static string VersionString(ReadOnlySpan<byte> v) => $"{v[3]}.{v[2]}.{v[1]}.{v[0]}";

    private static (byte[] Md5, uint Crc) Hash(FileStream fs, long offset, long size)
    {
        using var md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        var crc = new Crc32();
        fs.Position = offset;
        var buf = new byte[1 << 20];
        while (size > 0)
        {
            int n = fs.Read(buf, 0, (int)Math.Min(buf.Length, size));
            if (n <= 0)
                throw new EndOfStreamException();
            md5.AppendData(buf, 0, n);
            crc.Append(buf.AsSpan(0, n));
            size -= n;
        }
        return (md5.GetHashAndReset(), crc.GetCurrentHashAsUInt32());
    }

    private static void Copy(FileStream src, Stream dst, long offset, long size)
    {
        src.Position = offset;
        var buf = new byte[1 << 20];
        while (size > 0)
        {
            int n = src.Read(buf, 0, (int)Math.Min(buf.Length, size));
            if (n <= 0)
                throw new EndOfStreamException();
            dst.Write(buf, 0, n);
            size -= n;
        }
    }

    private static byte[] ReadAt(FileStream fs, long offset, int length)
    {
        var buf = new byte[length];
        fs.Position = offset;
        fs.ReadExactly(buf);
        return buf;
    }

    private static uint ReadUInt32(FileStream fs, long offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(ReadAt(fs, offset, 4));

    private static string CString(ReadOnlySpan<byte> data, int offset, int max)
    {
        var s = data.Slice(offset, max);
        int end = s.IndexOf((byte)0);
        return Encoding.ASCII.GetString(end < 0 ? s : s[..end]);
    }

    private static string Sanitize(string name) =>
        string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
}
