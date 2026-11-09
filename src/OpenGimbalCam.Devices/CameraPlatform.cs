using System.Collections.Concurrent;
using OpenGimbalCam.Core.Camera;
using OpenGimbalCam.Devices.Linux;
using OpenGimbalCam.Devices.Windows;

namespace OpenGimbalCam.Devices;

public static class CameraPlatform
{
    /// <summary>The camera backend for the running operating system.</summary>
    public static ICameraPlatform Create()
    {
        if (OperatingSystem.IsWindows())
        {
            // DirectShow/KS objects are created and used on one MTA thread, so callers on
            // any thread (an Avalonia STA UI thread, OSC socket callbacks) are safe.
            return new ThreadedPlatform(new DirectShowPlatform(), new WorkerThread("ogc-directshow"));
        }
        if (OperatingSystem.IsLinux())
            return new V4l2Platform();
        throw new PlatformNotSupportedException("Somente Windows (DirectShow) e Linux (V4L2) são suportados.");
    }
}

/// <summary>A thread that runs queued work in order; <see cref="Invoke{T}"/> blocks until done.</summary>
internal sealed class WorkerThread
{
    private readonly BlockingCollection<Action> _queue = [];
    private readonly Thread _thread;

    public WorkerThread(string name)
    {
        _thread = new Thread(Run) { IsBackground = true, Name = name };
        if (OperatingSystem.IsWindows())
            _thread.SetApartmentState(ApartmentState.MTA);
        _thread.Start();
    }

    public T Invoke<T>(Func<T> work)
    {
        if (Thread.CurrentThread == _thread)
            return work();
        var done = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _queue.Add(() =>
        {
            try
            {
                done.SetResult(work());
            }
            catch (Exception ex)
            {
                done.SetException(ex);
            }
        });
        return done.Task.GetAwaiter().GetResult();
    }

    public void Invoke(Action work) => Invoke(() =>
    {
        work();
        return true;
    });

    private void Run()
    {
        foreach (var work in _queue.GetConsumingEnumerable())
            work();
    }
}

internal sealed class ThreadedPlatform(ICameraPlatform inner, WorkerThread thread) : ICameraPlatform
{
    public string Name => inner.Name;

    public IReadOnlyList<CameraInfo> Enumerate() => thread.Invoke(inner.Enumerate);

    public ICameraSession Open(CameraInfo camera) => new ThreadedSession(thread.Invoke(() => inner.Open(camera)), thread);
}

internal sealed class ThreadedSession(ICameraSession inner, WorkerThread thread) : ICameraSession
{
    private ThreadedExtensionUnit? _xu;
    private bool _xuSearched;

    public CameraInfo Info => inner.Info;

    public ControlRange? GetRange(CameraControl control) => thread.Invoke(() => inner.GetRange(control));

    public ControlValue? Get(CameraControl control) => thread.Invoke(() => inner.Get(control));

    public void Set(CameraControl control, int value, ControlFlags flags = ControlFlags.Manual) =>
        thread.Invoke(() => inner.Set(control, value, flags));

    public IExtensionUnit? ExtensionUnit
    {
        get
        {
            if (!_xuSearched)
            {
                _xuSearched = true;
                _xu = thread.Invoke(() => inner.ExtensionUnit) is { } xu ? new ThreadedExtensionUnit(xu, thread) : null;
            }
            return _xu;
        }
    }

    public IReadOnlyList<string> DescribeTopology() => thread.Invoke(inner.DescribeTopology);

    // The session owns its extension unit; disposing it releases both on the worker thread.
    public void Dispose() => thread.Invoke(inner.Dispose);
}

internal sealed class ThreadedExtensionUnit(IExtensionUnit inner, WorkerThread thread) : IExtensionUnit
{
    public Guid Guid => inner.Guid;

    public string Location => inner.Location;

    public byte[] Get(uint selector, int length = ObsbotXu.PayloadLength) => thread.Invoke(() => inner.Get(selector, length));

    public void Set(uint selector, ReadOnlySpan<byte> payload, int length = ObsbotXu.PayloadLength)
    {
        var copy = payload.ToArray();
        thread.Invoke(() => inner.Set(selector, copy, length));
    }

    public void Dispose()
    {
        // Released with the session.
    }
}
