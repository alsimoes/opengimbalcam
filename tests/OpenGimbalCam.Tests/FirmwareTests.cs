using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using OpenGimbalCam.Core.Firmware;
using Xunit;

namespace OpenGimbalCam.Tests;

public class FirmwareTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ogcctl-tests-" + Guid.NewGuid().ToString("N"));

    public FirmwareTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Parses_and_verifies_synthetic_package()
    {
        var path = WritePackage(corrupt: false);
        var pkg = FirmwarePackage.Open(path);

        Assert.Equal("Obsbot_test", pkg.Product);
        Assert.Equal("OA_E", pkg.Region);
        Assert.Equal("1.2.6.2", pkg.Version);
        Assert.Equal("SSC339G", pkg.Chip);
        Assert.True(pkg.HeaderCrcOk);
        Assert.True(pkg.VerifyPayloadMd5());

        Assert.Equal(2, pkg.Components.Count);
        Assert.Equal("M_Camera_24.04.11.74.bin", pkg.Components[0].Name);
        Assert.Equal("24.4.11.74", pkg.Components[0].Version);
        Assert.Single(pkg.Partitions);
        Assert.Equal("KERNEL.bin", pkg.Partitions[0].Name);
        Assert.All(pkg.Verify(), r => Assert.True(r.Md5Ok && r.Crc32Ok != false && r.Entry.EntryCrcOk));

        var files = pkg.Extract(Path.Combine(_dir, "out"));
        Assert.Equal(3, files.Count);
        Assert.Equal(Kernel, File.ReadAllBytes(files[2]));
    }

    [Fact]
    public void Detects_corrupted_component()
    {
        var pkg = FirmwarePackage.Open(WritePackage(corrupt: true));
        Assert.Contains(pkg.Verify(), r => !r.Md5Ok);
    }

    [Fact]
    public void Real_package_if_available()
    {
        // Optional: validates the vendor file when the repo has original-driver/ checked out locally.
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "original-driver", "Obsbot_tiny_2P_OA_E_All_1.2.6.2_release.bin"));
        if (!File.Exists(path))
            return;
        var pkg = FirmwarePackage.Open(path);
        Assert.Equal("Obsbot_tiny_2P", pkg.Product);
        Assert.Equal(3, pkg.Components.Count);
        Assert.Equal(8, pkg.Partitions.Count);
        Assert.All(pkg.Verify(), r => Assert.True(r.Md5Ok && r.Crc32Ok != false && r.Entry.EntryCrcOk));
    }

    private static readonly byte[] Kernel = Encoding.ASCII.GetBytes("kernel-image-bytes");
    private static readonly byte[] Mcu = Encoding.ASCII.GetBytes("gimbal-mcu-firmware");

    private string WritePackage(bool corrupt)
    {
        // Inner AMAC container: 0x200 header (1 entry) + kernel.
        var amac = new byte[0x200 + Kernel.Length];
        Encoding.ASCII.GetBytes("AMAC").CopyTo(amac, 0);
        Le(amac, 4, 0x200);
        Le(amac, 12, 1);
        var ae = amac.AsSpan(0x100, 0x100);
        Le(ae, 0, 4);
        Le(ae, 4, (uint)Kernel.Length);
        Le(ae, 8, 0x200);
        Encoding.ASCII.GetBytes("KERNEL.bin").CopyTo(ae[0x1C..]);
        MD5.HashData(Kernel).CopyTo(ae[0x44..]);
        Kernel.CopyTo(amac, 0x200);
        Seal(ae);

        // Outer package: 0x400 header (2 entries) + AMAC + MCU blob.
        var file = new byte[0x400 + amac.Length + Mcu.Length];
        Le(file, 0, FirmwarePackage.Magic);
        Le(file, 4, 0x400);
        Le(file, 8, (uint)(amac.Length + Mcu.Length));
        Le(file, 12, 2);
        Encoding.ASCII.GetBytes("Obsbot_test").CopyTo(file, 0x10);
        Encoding.ASCII.GetBytes("OA_E").CopyTo(file, 0x20);
        new byte[] { 2, 6, 2, 1 }.CopyTo(file, 0x30);
        Encoding.ASCII.GetBytes("SSC339G").CopyTo(file, 0x48);
        Encoding.ASCII.GetBytes("release").CopyTo(file, 0x58);
        amac.CopyTo(file, 0x400);
        Mcu.CopyTo(file, 0x400 + amac.Length);
        AddEntry(file, 0, 1, 0x400, amac, "M_Camera_24.04.11.74.bin", [0x4A, 0x0B, 0x04, 0x18]);
        AddEntry(file, 1, 2, (uint)(0x400 + amac.Length), Mcu, "M_Gimbal_1.0.17.1.bin", [0x01, 0x11, 0x00, 0x01]);
        MD5.HashData(file.AsSpan(0x400)).CopyTo(file, 0x38);
        Le(file, 0xFC, Crc(file.AsSpan(0, 0xFC)));

        if (corrupt)
            file[^1] ^= 0xFF;
        var path = Path.Combine(_dir, corrupt ? "bad.bin" : "good.bin");
        File.WriteAllBytes(path, file);
        return path;
    }

    private static void AddEntry(byte[] file, int index, uint type, uint offset, byte[] data, string name, byte[] version)
    {
        var e = file.AsSpan(0x100 + index * 0x100, 0x100);
        Le(e, 0, type);
        Le(e, 4, (uint)data.Length);
        Le(e, 8, offset);
        version.CopyTo(e[0x3C..]);
        Le(e, 0x44, Crc(data));
        MD5.HashData(data).CopyTo(e[0x48..]);
        Encoding.ASCII.GetBytes(name).CopyTo(e[0x58..]);
        Seal(e);
    }

    private static void Seal(Span<byte> entry) => Le(entry, 0xFC, Crc(entry[..0xFC]));

    private static uint Crc(ReadOnlySpan<byte> data) => Crc32.HashToUInt32(data);

    private static void Le(Span<byte> buf, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(buf[offset..], value);
}
