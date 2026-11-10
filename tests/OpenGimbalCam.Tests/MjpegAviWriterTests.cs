using System.Text;
using OpenGimbalCam.Core.Audio;
using OpenGimbalCam.Studio;
using Xunit;

namespace OpenGimbalCam.Tests;

public class MjpegAviWriterTests
{
    [Fact]
    public void WritesAConsistentRiffWithIndex()
    {
        string path = Path.Combine(Path.GetTempPath(), $"opengimbalcam-test-{Guid.NewGuid():N}.avi");
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

    [Fact]
    public void WithoutAudioHasOneStreamAndNoAudioStrl()
    {
        string path = Temp();
        try
        {
            using (var w = new MjpegAviWriter(path, 320, 240, 30))
            {
                w.WriteAudio(new byte[400]); // ignored: no audio stream
                w.WriteFrame([0xFF, 0xD8, 0xFF, 0xD9]);
            }
            var b = File.ReadAllBytes(path);
            Assert.Equal(1, BitConverter.ToInt32(b, IndexOf(b, "avih") + 8 + 24));
            Assert.Equal(-1, IndexOf(b, "auds"));
            Assert.Equal(-1, IndexOf(b, "01wb"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void WithAudioWritesSecondStreamChunksAndIndex()
    {
        string path = Temp();
        try
        {
            var fmt = new AudioFormat(48000, 2); // blockAlign 4
            using (var w = new MjpegAviWriter(path, 640, 360, 30, fmt))
            {
                w.WriteAudio(new byte[40]); // before the first frame: dropped
                w.WriteFrame([0xFF, 0xD8, 1, 0xFF, 0xD9]);
                w.WriteAudio(new byte[8]);
                w.WriteAudio(new byte[13]); // trailing partial sample frame is trimmed to 12
                w.WriteFrame([0xFF, 0xD8, 0xFF, 0xD9]);
                w.WriteAudio(new byte[4]);
            }
            var b = File.ReadAllBytes(path);
            Assert.Equal(b.Length - 8, BitConverter.ToInt32(b, 4));

            int avih = IndexOf(b, "avih") + 8;
            Assert.Equal(2, BitConverter.ToInt32(b, avih + 24)); // dwStreams
            Assert.Equal(2, BitConverter.ToInt32(b, avih + 16)); // video frames

            int auds = IndexOf(b, "auds");
            int strh = auds; // fccType at start of strh payload
            Assert.Equal(0, BitConverter.ToInt32(b, strh + 4));
            Assert.Equal(4, BitConverter.ToInt32(b, strh + 20));          // dwScale = blockAlign
            Assert.Equal(48000 * 4, BitConverter.ToInt32(b, strh + 24));  // dwRate
            Assert.Equal(6, BitConverter.ToInt32(b, strh + 32));          // (8 + 12 + 4) / 4 sample frames
            Assert.Equal(12, BitConverter.ToInt32(b, strh + 36));         // suggested buffer
            Assert.Equal(4, BitConverter.ToInt32(b, strh + 44));          // dwSampleSize

            int strf = IndexOf(b, "strf", strh) + 8;
            Assert.Equal(1, BitConverter.ToInt16(b, strf));
            Assert.Equal(2, BitConverter.ToInt16(b, strf + 2));
            Assert.Equal(48000, BitConverter.ToInt32(b, strf + 4));
            Assert.Equal(48000 * 4, BitConverter.ToInt32(b, strf + 8));
            Assert.Equal(4, BitConverter.ToInt16(b, strf + 12));
            Assert.Equal(16, BitConverter.ToInt16(b, strf + 14));

            int movi = IndexOf(b, "movi");
            int moviList = movi - 8;
            int idx1 = moviList + 8 + BitConverter.ToInt32(b, moviList + 4);
            Assert.Equal("idx1", Ascii(b, idx1));
            Assert.Equal(5 * 16, BitConverter.ToInt32(b, idx1 + 4));
            string[] ids = ["00dc", "01wb", "01wb", "00dc", "01wb"];
            int[] sizes = [5, 8, 12, 4, 4];
            for (int i = 0; i < 5; i++)
            {
                int entry = idx1 + 8 + i * 16;
                Assert.Equal(ids[i], Ascii(b, entry));
                int chunk = movi + BitConverter.ToInt32(b, entry + 8);
                Assert.Equal(ids[i], Ascii(b, chunk));
                Assert.Equal(sizes[i], BitConverter.ToInt32(b, chunk + 4));
                Assert.Equal(sizes[i], BitConverter.ToInt32(b, entry + 12));
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ComputeFpsUsesIntervalsBetweenFirstAndLastFrame()
    {
        Assert.Equal(25, MjpegAviWriter.ComputeFps(51, 2.0), 6);
        Assert.Equal(0, MjpegAviWriter.ComputeFps(1, 2.0));
        Assert.Equal(0, MjpegAviWriter.ComputeFps(10, 0));
    }

    private static string Temp() => Path.Combine(Path.GetTempPath(), $"opengimbalcam-test-{Guid.NewGuid():N}.avi");

    private static string Ascii(byte[] b, int offset, int length = 4) => Encoding.ASCII.GetString(b, offset, length);

    private static int IndexOf(byte[] b, string fourcc, int from = 0)
    {
        int i = b.AsSpan(from).IndexOf(Encoding.ASCII.GetBytes(fourcc));
        return i < 0 ? -1 : i + from;
    }
}
