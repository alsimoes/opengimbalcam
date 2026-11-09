using System.Diagnostics;
using System.Text;

namespace OpenGimbalCam.Studio;

/// <summary>
/// Writes JPEG frames as an AVI 1.0 file with one MJPG video stream (RIFF "AVI ",
/// hdrl/avih/strl, movi with "00dc" chunks, idx1). The frame rate is measured from
/// the arrival times, since the camera may deliver fewer frames than its nominal rate.
/// </summary>
internal sealed class MjpegAviWriter : IDisposable
{
    private const int AvihFlagsHasIndex = 0x10;
    private const int IndexFlagKeyFrame = 0x10;

    private readonly FileStream _file;
    private readonly BinaryWriter _w;
    private readonly List<(int Offset, int Size)> _index = [];
    private readonly Stopwatch _clock = new();
    private readonly long _riffSizePos, _avihPos, _strhPos, _moviSizePos, _moviStart;
    private int _maxFrame;

    public MjpegAviWriter(string path, int width, int height, double nominalFps)
    {
        Path = path;
        _file = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite);
        _w = new BinaryWriter(_file, Encoding.ASCII, leaveOpen: true);

        Fourcc("RIFF");
        _riffSizePos = Placeholder();
        Fourcc("AVI ");

        Fourcc("LIST");
        long hdrlSize = Placeholder();
        Fourcc("hdrl");

        Fourcc("avih");
        _w.Write(56);
        _avihPos = _file.Position;
        _w.Write((int)(1_000_000 / Math.Max(nominalFps, 1))); // dwMicroSecPerFrame
        _w.Write(0);                                           // dwMaxBytesPerSec
        _w.Write(0);                                           // dwPaddingGranularity
        _w.Write(AvihFlagsHasIndex);
        _w.Write(0);                                           // dwTotalFrames
        _w.Write(0);                                           // dwInitialFrames
        _w.Write(1);                                           // dwStreams
        _w.Write(0);                                           // dwSuggestedBufferSize
        _w.Write(width);
        _w.Write(height);
        _w.Write(new byte[16]);

        Fourcc("LIST");
        long strlSize = Placeholder();
        Fourcc("strl");

        Fourcc("strh");
        _w.Write(56);
        _strhPos = _file.Position;
        Fourcc("vids");
        Fourcc("MJPG");
        _w.Write(0);                                           // dwFlags
        _w.Write(0);                                           // wPriority, wLanguage
        _w.Write(0);                                           // dwInitialFrames
        _w.Write(1000);                                        // dwScale
        _w.Write((int)Math.Round(nominalFps * 1000));          // dwRate
        _w.Write(0);                                           // dwStart
        _w.Write(0);                                           // dwLength
        _w.Write(0);                                           // dwSuggestedBufferSize
        _w.Write(-1);                                          // dwQuality
        _w.Write(0);                                           // dwSampleSize
        _w.Write((short)0);
        _w.Write((short)0);
        _w.Write((short)width);
        _w.Write((short)height);

        Fourcc("strf");
        _w.Write(40);
        _w.Write(40);                                          // BITMAPINFOHEADER.biSize
        _w.Write(width);
        _w.Write(height);
        _w.Write((short)1);
        _w.Write((short)24);
        Fourcc("MJPG");
        _w.Write(width * height * 3);
        _w.Write(new byte[16]);

        Patch(strlSize, _file.Position - strlSize - 4);
        Patch(hdrlSize, _file.Position - hdrlSize - 4);

        Fourcc("LIST");
        _moviSizePos = Placeholder();
        _moviStart = _file.Position;
        Fourcc("movi");
    }

    public string Path { get; }

    public int Frames => _index.Count;

    public void WriteFrame(ReadOnlySpan<byte> jpeg)
    {
        _clock.Start();
        _index.Add(((int)(_file.Position - _moviStart), jpeg.Length));
        Fourcc("00dc");
        _w.Write(jpeg.Length);
        _w.Write(jpeg);
        if ((jpeg.Length & 1) != 0)
            _w.Write((byte)0);
        _maxFrame = Math.Max(_maxFrame, jpeg.Length);
    }

    public void Dispose()
    {
        Patch(_moviSizePos, _file.Position - _moviSizePos - 4);

        Fourcc("idx1");
        _w.Write(_index.Count * 16);
        foreach (var (offset, size) in _index)
        {
            Fourcc("00dc");
            _w.Write(IndexFlagKeyFrame);
            _w.Write(offset);
            _w.Write(size);
        }
        Patch(_riffSizePos, _file.Position - 8);

        double fps = _index.Count > 1 && _clock.Elapsed.TotalSeconds > 0
            ? (_index.Count - 1) / _clock.Elapsed.TotalSeconds
            : 0;
        _file.Position = _avihPos;
        if (fps > 0)
            _w.Write((int)(1_000_000 / fps));
        _file.Position = _avihPos + 16;
        _w.Write(_index.Count);
        _file.Position = _avihPos + 28;
        _w.Write(_maxFrame);

        if (fps > 0)
        {
            _file.Position = _strhPos + 24;
            _w.Write((int)Math.Round(fps * 1000));
        }
        _file.Position = _strhPos + 32;
        _w.Write(_index.Count);
        _w.Write(_maxFrame);

        _w.Dispose();
        _file.Dispose();
    }

    private void Fourcc(string code) => _w.Write(Encoding.ASCII.GetBytes(code));

    private long Placeholder()
    {
        long pos = _file.Position;
        _w.Write(0);
        return pos;
    }

    private void Patch(long pos, long value)
    {
        long here = _file.Position;
        _file.Position = pos;
        _w.Write(checked((int)value));
        _file.Position = here;
    }
}
