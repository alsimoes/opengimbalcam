namespace OpenGimbalCam.Core.Audio;

/// <summary>Format of the PCM buffers delivered by an <see cref="IAudioCapture"/>: always 16-bit signed little-endian.</summary>
public readonly record struct AudioFormat(int SampleRate, int Channels)
{
    public const int BitsPerSample = 16;

    /// <summary>Bytes of one sample frame (one sample of every channel).</summary>
    public int BlockAlign => Channels * BitsPerSample / 8;

    public int BytesPerSecond => SampleRate * BlockAlign;
}

/// <summary>Microphone capture. Buffers arrive on a driver thread; handlers must not block.</summary>
public interface IAudioCapture : IDisposable
{
    AudioFormat Format { get; }

    /// <summary>Raised with whole sample frames of 16-bit PCM; the memory is only valid during the call.</summary>
    event Action<ReadOnlyMemory<byte>>? DataAvailable;

    void Start();

    void Stop();
}
