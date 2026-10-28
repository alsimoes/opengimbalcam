using System.Text;
using ObsbotStudio;
using Xunit;

namespace Obsbot.Tests;

public class MjpegAviWriterTests
{
    [Fact]
    public void WritesAConsistentRiffWithIndex()
    {
        string path = Path.Combine(Path.GetTempPath(), $"obsbot-test-{Guid.NewGuid():N}.avi");
        try
        {
            using (var w = new MjpegAviWriter(path, 640, 360, 30))
            {
                w.WriteFrame([0xFF, 0xD8, 1, 0xFF, 0xD9]); // odd length: padded
                w.WriteFrame([0xFF, 0xD8, 1, 2, 0xFF, 0xD9]);
                w.WriteFrame([0xFF, 0xD8, 0xFF, 0xD9]);
            }

            var b = File.ReadAllBytes(path);
            Assert.Equal("RIFF", Ascii(b, 0));
            Assert.Equal(b.Length - 8, BitConverter.ToInt32(b, 4));
            Assert.Equal("AVI ", Ascii(b, 8));

            int avih = IndexOf(b, "avih") + 8;
            Assert.Equal(3, BitConverter.ToInt32(b, avih + 16)); // dwTotalFrames
            Assert.Equal(640, BitConverter.ToInt32(b, avih + 32));
            Assert.Equal(360, BitConverter.ToInt32(b, avih + 36));

            int strh = IndexOf(b, "strh") + 8;
            Assert.Equal("vidsMJPG", Ascii(b, strh, 8));
            Assert.Equal(3, BitConverter.ToInt32(b, strh + 32)); // dwLength

            int movi = IndexOf(b, "movi");
            int moviList = movi - 8;
            int idx1 = moviList + 8 + BitConverter.ToInt32(b, moviList + 4);
            Assert.Equal("idx1", Ascii(b, idx1));
            Assert.Equal(3 * 16, BitConverter.ToInt32(b, idx1 + 4));

            // every index entry points at a "00dc" chunk of the recorded size, relative to "movi"
            int[] sizes = [5, 6, 4];
            for (int i = 0; i < 3; i++)
            {
                int entry = idx1 + 8 + i * 16;
                int chunk = movi + BitConverter.ToInt32(b, entry + 8);
                Assert.Equal("00dc", Ascii(b, chunk));
                Assert.Equal(sizes[i], BitConverter.ToInt32(b, chunk + 4));
                Assert.Equal(sizes[i], BitConverter.ToInt32(b, entry + 12));
                Assert.Equal(0xFF, b[chunk + 8]);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string Ascii(byte[] b, int offset, int length = 4) => Encoding.ASCII.GetString(b, offset, length);

    private static int IndexOf(byte[] b, string fourcc) => b.AsSpan().IndexOf(Encoding.ASCII.GetBytes(fourcc));
}
