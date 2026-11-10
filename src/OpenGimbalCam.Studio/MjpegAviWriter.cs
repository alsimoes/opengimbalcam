using System.Diagnostics;
using System.Text;
using OpenGimbalCam.Core.Audio;

namespace OpenGimbalCam.Studio;

/// <summary>
/// Writes JPEG frames as an AVI 1.0 file with one MJPG video stream (RIFF "AVI ",
/// hdrl/avih/strl, movi with "00dc" chunks, idx1). The frame rate is measured from
/// the arrival times, since the camera may deliver fewer frames than its nominal rate.
/// With an <see cref="AudioFormat"/> a second PCM stream ('01wb' chunks) is interleaved; audio
/// received before the first video frame is dropped, and at finalize the video rate is patched to
/// the measured fps so the video length matches the audio. Thread-safe: audio and video arrive on
/// different threads.
/// </summary>
internal sealed class MjpegAviWriter : IDisposable
{
    private const int AvihFlagsHasIndex = 0x10;
    private const int IndexFlagKeyFrame = 0x10;

    private readonly FileStream _file;
    private readonly BinaryWriter _w;
    private readonly List<(bool Audio, int Offset, int Size)> _entries = [];
    private readonly object _lock = new();
    private readonly AudioFormat? _audio;
    private readonly long _riffSizePos, _avihPos, _strhPos, _audioStrhPos, _moviSizePos, _moviStart;
    private long _firstTicks, _lastTicks;
    private long _audioBytes;
    private int _maxFrame, _maxAudio, _videoFrames;

    public MjpegAviWriter(string path, int width, int height, double nominalFps, AudioFormat? audio = null)
    {
        _audio = audio;
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
        _w.Write(audio is null ? 1 : 2);                         // dwStreams
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

        if (audio is { } a)
        {
            Fourcc("LIST");
            long astrlSize = Placeholder();
            Fourcc("strl");

            Fourcc("strh");
            _w.Write(56);
            _audioStrhPos = _file.Position;
            Fourcc("auds");
            _w.Write(0);                                       // fccHandler
            _w.Write(0);                                       // dwFlags
            _w.Write(0);                                       // wPriority, wLanguage
            _w.Write(0);                                       // dwInitialFrames
            _w.Write(a.BlockAlign);                            // dwScale
            _w.Write(a.BytesPerSecond);                        // dwRate
            _w.Write(0);                                       // dwStart
            _w.Write(0);                                       // dwLength (sample frames)
            _w.Write(0);                                       // dwSuggestedBufferSize
            _w.Write(-1);                                      // dwQuality
            _w.Write(a.BlockAlign);                            // dwSampleSize
            _w.Write(new byte[8]);                             // rcFrame

            Fourcc("strf");
            _w.Write(18);
            _w.Write((short)1);                                // WAVE_FORMAT_PCM
            _w.Write((short)a.Channels);
            _w.Write(a.SampleRate);
            _w.Write(a.BytesPerSecond);
            _w.Write((short)a.BlockAlign);
            _w.Write((short)AudioFormat.BitsPerSample);
            _w.Write((short)0);                                // cbSize
            Patch(astrlSize, _file.Position - astrlSize - 4);
        }

        Patch(hdrlSize, _file.Position - hdrlSize - 4);

        Fourcc("LIST");
        _moviSizePos = Placeholder();
        _moviStart = _file.Position;
        Fourcc("movi");
    }

    public string Path { get; }

    public int Frames => Volatile.Read(ref _videoFrames);

    /// <summary>Total audio sample frames written so far.</summary>
    public long AudioSampleFrames => _audio is { } a ? Volatile.Read(ref _audioBytes) / a.BlockAlign : 0;

    /// <summary>Measured video rate: n frames spanning (n-1) intervals between first and last arrival.</summary>
    internal static double ComputeFps(int frames, double seconds) =>
        frames > 1 && seconds > 0 ? (frames - 1) / seconds : 0;

    public void WriteFrame(ReadOnlySpan<byte> jpeg)
    {
        lock (_lock)
        {
            long now = Stopwatch.GetTimestamp();
            if (_videoFrames == 0)
                _firstTicks = now;
            _lastTicks = now;
            _videoFrames++;
            WriteChunk("00dc", jpeg, audio: false);
            _maxFrame = Math.Max(_maxFrame, jpeg.Length);
        }
    }

    /// <summary>Appends 16-bit PCM; dropped until the first video frame has been written.</summary>
    public void WriteAudio(ReadOnlySpan<byte> pcm)
    {
        if (_audio is not { } a)
            return;
        lock (_lock)
        {
            if (_videoFrames == 0)
                return;
            pcm = pcm[..(pcm.Length / a.BlockAlign * a.BlockAlign)];
            if (pcm.IsEmpty)
                return;
            WriteChunk("01wb", pcm, audio: true);
            _audioBytes += pcm.Length;
            _maxAudio = Math.Max(_maxAudio, pcm.Length);
        }
    }

    private void WriteChunk(string id, ReadOnlySpan<byte> data, bool audio)
    {
        _entries.Add((audio, (int)(_file.Position - _moviStart), data.Length));
        Fourcc(id);
        _w.Write(data.Length);
        _w.Write(data);
        if ((data.Length & 1) != 0)
            _w.Write((byte)0);
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (!_file.CanWrite)
                return;
            Patch(_moviSizePos, _file.Position - _moviSizePos - 4);

            Fourcc("idx1");
            _w.Write(_entries.Count * 16);
            foreach (var (audio, offset, size) in _entries)
            {
                Fourcc(audio ? "01wb" : "00dc");
                _w.Write(IndexFlagKeyFrame);
                _w.Write(offset);
                _w.Write(size);
            }
            Patch(_riffSizePos, _file.Position - 8);

            double fps = ComputeFps(_videoFrames, (_lastTicks - _firstTicks) / (double)Stopwatch.Frequency);
            _file.Position = _avihPos;
            if (fps > 0)
                _w.Write((int)(1_000_000 / fps));
            _file.Position = _avihPos + 16;
            _w.Write(_videoFrames);
            _file.Position = _avihPos + 28;
            _w.Write(Math.Max(_maxFrame, _maxAudio));

            if (fps > 0)
            {
                _file.Position = _strhPos + 24;
                _w.Write((int)Math.Round(fps * 1000));
            }
            _file.Position = _strhPos + 32;
            _w.Write(_videoFrames);
            _w.Write(_maxFrame);

            if (_audio is { } a)
            {
                _file.Position = _audioStrhPos + 32;
                _w.Write((int)(_audioBytes / a.BlockAlign));
                _w.Write(_maxAudio);
            }

            _w.Dispose();
            _file.Dispose();
        }
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
