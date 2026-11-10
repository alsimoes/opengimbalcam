using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Dmo;
using OpenGimbalCam.Core.Audio;
using OpenGimbalCam.Core.Camera;

namespace OpenGimbalCam.Devices.Windows;

/// <summary>WASAPI shared-mode capture of the OBSBOT Tiny microphone, converted to 16-bit PCM.</summary>
[SupportedOSPlatform("windows")]
internal sealed class WasapiTinyCapture : IAudioCapture
{
    private readonly WasapiCapture _capture;
    private readonly MMDevice _device;
    private readonly bool _isFloat;
    private readonly int _srcBits;
    private byte[] _buffer = [];

    private WasapiTinyCapture(MMDevice device)
    {
        _device = device;
        _capture = new WasapiCapture(device);
        var wf = _capture.WaveFormat;
        _srcBits = wf.BitsPerSample;
        _isFloat = wf.Encoding == WaveFormatEncoding.IeeeFloat
            || (wf is WaveFormatExtensible ext && ext.SubFormat == AudioMediaSubtypes.MEDIASUBTYPE_IEEE_FLOAT);
        Format = new AudioFormat(wf.SampleRate, wf.Channels);
        _capture.DataAvailable += OnData;
    }

    public AudioFormat Format { get; }

    public event Action<ReadOnlyMemory<byte>>? DataAvailable;

    /// <summary>Finds the active capture endpoint of the Tiny, or null.</summary>
    public static WasapiTinyCapture? TryCreate()
    {
        using var enumerator = new MMDeviceEnumerator();
        var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active).ToList();
        MMDevice? match = devices.FirstOrDefault(IsTinyByHardwareId)
            ?? devices.FirstOrDefault(d => Safe(() => d.FriendlyName).Contains("OBSBOT Tiny", StringComparison.OrdinalIgnoreCase));
        foreach (var d in devices.Where(d => d != match))
            d.Dispose();
        return match is null ? null : new WasapiTinyCapture(match);
    }

    public void Start() => _capture.StartRecording();

    public void Stop()
    {
        try
        {
            _capture.StopRecording();
        }
        catch (Exception)
        {
            // Already stopped or device removed: nothing to release.
        }
    }

    public void Dispose()
    {
        Stop();
        _capture.DataAvailable -= OnData;
        _capture.Dispose();
        _device.Dispose();
    }

    // Any string property of the endpoint (instance id, controller id, ...) that carries the Tiny VID/PID.
    private static bool IsTinyByHardwareId(MMDevice device)
    {
        if (TinyIdentity.IsMatchDevicePath(Safe(() => device.Properties[PropertyKeys.PKEY_Device_InstanceId].Value as string)))
            return true;
        if (TinyIdentity.IsMatchDevicePath(Safe(() => device.Properties[PropertyKeys.PKEY_Device_ControllerDeviceId].Value as string)))
            return true;
        return TinyIdentity.IsMatchDevicePath(device.ID);
    }

    private static string Safe(Func<string?> read)
    {
        try
        {
            return read() ?? "";
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or ArgumentException or KeyNotFoundException)
        {
            return "";
        }
    }

    private void OnData(object? sender, WaveInEventArgs e)
    {
        int bytes = e.BytesRecorded;
        if (bytes <= 0)
            return;
        int srcBytes = _srcBits / 8;
        int samples = bytes / srcBytes;
        int outLen = samples * 2;
        if (_buffer.Length < outLen)
            _buffer = new byte[outLen];
        var src = e.Buffer;
        for (int i = 0; i < samples; i++)
        {
            int o = i * 2;
            short s;
            if (_isFloat && srcBytes == 4)
            {
                float f = BitConverter.ToSingle(src, i * 4);
                s = (short)Math.Clamp((int)Math.Round(f * 32768f), short.MinValue, short.MaxValue);
            }
            else
            {
                int p = i * srcBytes;
                s = srcBytes switch
                {
                    2 => BitConverter.ToInt16(src, p),
                    3 => (short)(src[p + 1] | (src[p + 2] << 8)),
                    4 => (short)(BitConverter.ToInt32(src, p) >> 16),
                    _ => 0,
                };
            }
            _buffer[o] = (byte)s;
            _buffer[o + 1] = (byte)(s >> 8);
        }
        DataAvailable?.Invoke(_buffer.AsMemory(0, outLen));
    }
}
