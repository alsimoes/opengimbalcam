using Avalonia.Media.Imaging;
using FlashCap;
using Obsbot.Core.Camera;

namespace ObsbotStudio;

/// <summary>
/// Live video of one camera through FlashCap (DirectShow on Windows, V4L2 on Linux).
/// Streaming also keeps OBSBOT cameras awake: asleep, they accept gimbal commands but do not move.
/// </summary>
internal sealed class CameraPreview : IAsyncDisposable
{
    private const int MaxWidth = 1280;
    private const double MaxFps = 30;

    private readonly Action<Bitmap> _onFrame;
    private readonly object _recordLock = new();
    private CaptureDevice? _device;
    private VideoCharacteristics? _format;
    private MjpegAviWriter? _recorder;
    private byte[]? _lastImage;
    private int _busy;

    private CameraPreview(Action<Bitmap> onFrame) => _onFrame = onFrame;

    /// <summary>e.g. "JPEG 1280x720 @ 30 fps".</summary>
    public string Format { get; private set; } = "";

    /// <summary>
    /// Opens the capture device matching <paramref name="camera"/>. Frames are decoded on the
    /// capture thread; a new one is only decoded after <see cref="FrameShown"/> (drops frames
    /// instead of queuing them when the UI is slower than the camera).
    /// </summary>
    public static async Task<CameraPreview> StartAsync(CameraInfo camera, Action<Bitmap> onFrame, CancellationToken ct)
    {
        var descriptors = new CaptureDevices().GetDescriptors();
        var descriptor = descriptors.FirstOrDefault(d => camera.Path is not null
                && string.Equals(d.Identity?.ToString(), camera.Path, StringComparison.OrdinalIgnoreCase))
            ?? descriptors.FirstOrDefault(d => d.DeviceType != DeviceTypes.VideoForWindows && d.Name == camera.Name)
            ?? throw new IOException($"{camera.Name}: dispositivo de captura não encontrado.");
        var format = PickFormat(descriptor.Characteristics)
            ?? throw new NotSupportedException($"{camera.Name}: nenhum formato de vídeo suportado.");

        var preview = new CameraPreview(onFrame) { Format = Describe(format), _format = format };
        preview._device = await descriptor.OpenAsync(format, preview.OnPixelBuffer, ct);
        await preview._device.StartAsync(ct);
        return preview;
    }

    public void FrameShown() => Volatile.Write(ref _busy, 0);

    public bool IsRecording => Volatile.Read(ref _recorder) is not null;

    /// <summary>Saves the latest frame (JPEG, or BMP for raw formats) and returns its path.</summary>
    public string SaveSnapshot(string directory)
    {
        var image = Volatile.Read(ref _lastImage) ?? throw new InvalidOperationException("nenhum quadro recebido ainda.");
        Directory.CreateDirectory(directory);
        string ext = _format?.PixelFormat == PixelFormats.JPEG ? "jpg" : "bmp";
        string path = System.IO.Path.Combine(directory, $"obsbot-{DateTime.Now:yyyyMMdd-HHmmss-fff}.{ext}");
        File.WriteAllBytes(path, image);
        return path;
    }

    /// <summary>Starts writing every frame to an MJPEG AVI; only for JPEG streams.</summary>
    public string StartRecording(string directory)
    {
        if (_format is not { PixelFormat: PixelFormats.JPEG } f)
            throw new NotSupportedException($"gravação só com vídeo MJPEG (o formato aberto é {Format}).");
        Directory.CreateDirectory(directory);
        string path = System.IO.Path.Combine(directory, $"obsbot-{DateTime.Now:yyyyMMdd-HHmmss}.avi");
        lock (_recordLock)
        {
            if (_recorder is not null)
                return _recorder.Path;
            _recorder = new MjpegAviWriter(path, f.Width, f.Height, Fps(f));
        }
        return path;
    }

    /// <summary>Finishes the AVI; returns its path and frame count, or null when not recording.</summary>
    public (string Path, int Frames)? StopRecording()
    {
        lock (_recordLock)
        {
            if (_recorder is not { } r)
                return null;
            _recorder = null;
            r.Dispose();
            return (r.Path, r.Frames);
        }
    }

    public async ValueTask DisposeAsync()
    {
        StopRecording();
        if (_device is null)
            return;
        var device = _device;
        _device = null;
        try
        {
            await device.StopAsync(CancellationToken.None);
        }
        finally
        {
            await device.DisposeAsync();
        }
    }

    private void OnPixelBuffer(PixelBufferScope scope)
    {
        // Recording takes every frame; the preview only the ones the UI has time for.
        bool recording = IsRecording;
        bool show = Interlocked.Exchange(ref _busy, 1) == 0;
        if (!recording && !show)
            return;
        try
        {
            // JPEG for MJPEG streams, BMP for transcoded raw formats: Avalonia decodes both.
            var image = scope.Buffer.ExtractImage();
            Volatile.Write(ref _lastImage, image);
            if (recording)
            {
                lock (_recordLock)
                    _recorder?.WriteFrame(image);
            }
            if (show)
            {
                using var stream = new MemoryStream(image);
                _onFrame(new Bitmap(stream));
            }
        }
        catch (Exception)
        {
            if (show)
                FrameShown();
        }
    }

    /// <summary>MJPEG first (cheap to transfer and decode), largest width up to 1280, highest fps up to 30.</summary>
    internal static VideoCharacteristics? PickFormat(IEnumerable<VideoCharacteristics> formats) =>
        formats
            .Where(f => f.PixelFormat != PixelFormats.Unknown && f.Width > 0)
            .OrderByDescending(f => f.PixelFormat == PixelFormats.JPEG)
            .ThenByDescending(f => f.Width <= MaxWidth)
            .ThenBy(f => f.Width <= MaxWidth ? -f.Width : f.Width)
            .ThenByDescending(f => Math.Min(Fps(f), MaxFps))
            .FirstOrDefault();

    private static double Fps(VideoCharacteristics f) =>
        f.FramesPerSecond.Denominator == 0 ? 0 : (double)f.FramesPerSecond.Numerator / f.FramesPerSecond.Denominator;

    private static string Describe(VideoCharacteristics f) => $"{f.PixelFormat} {f.Width}x{f.Height} @ {Fps(f):0.#} fps";
}
