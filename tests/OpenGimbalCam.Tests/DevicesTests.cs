using OpenGimbalCam.Core.Camera;
using OpenGimbalCam.Devices;
using OpenGimbalCam.Devices.Linux;
using Xunit;

namespace OpenGimbalCam.Tests;

public class UvcDescriptorTests
{
    // Device + configuration descriptors trimmed to what the parser looks at:
    // a VideoControl interface (class 0x0E, subclass 1) with a header, camera terminal,
    // processing unit and the OBSBOT XU, then a VideoStreaming interface whose
    // VS_FORMAT_MJPEG descriptor also has subtype 0x06 and must be ignored.
    private static byte[] SampleDescriptors()
    {
        var d = new List<byte>();
        d.AddRange([18, 0x01, 0x00, 0x02, 0xEF, 0x02, 0x01, 0x40, 0x64, 0x35, 0xF0, 0xFE, 0x00, 0x01, 1, 2, 3, 1]);
        d.AddRange([9, 0x02, 0x00, 0x01, 2, 1, 0, 0x80, 250]);
        d.AddRange([9, 0x04, 0, 0, 1, 0x0E, 0x01, 0x00, 0]); // VC interface
        d.AddRange([13, 0x24, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01]); // VC header
        d.AddRange([18, 0x24, 0x02, 1, 0x01, 0x02, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0x0A, 0x00, 0x00]); // camera terminal
        d.AddRange([11, 0x24, 0x05, 2, 1, 0, 0, 2, 0x5B, 0x17, 0]); // processing unit
        var xu = new List<byte> { 0, 0x24, 0x06, 6 };
        xu.AddRange(ObsbotXu.Guid.ToByteArray());
        xu.AddRange([7, 1, 2, 1, 0xFF, 0]); // bNumControls, bNrInPins, baSourceID, bControlSize, bmControls, iExtension
        xu[0] = (byte)xu.Count;
        d.AddRange(xu);
        d.AddRange([9, 0x04, 1, 0, 1, 0x0E, 0x02, 0x00, 0]); // VS interface
        d.AddRange([11, 0x24, 0x06, 1, 1, 0x01, 1, 0, 0, 0, 0]); // VS_FORMAT_MJPEG
        return [.. d];
    }

    [Fact]
    public void FindsObsbotExtensionUnitId()
    {
        Assert.Equal((byte)6, UvcDescriptors.FindExtensionUnit(SampleDescriptors(), ObsbotXu.Guid));
    }

    [Fact]
    public void ListsVideoControlUnitsOnly()
    {
        var units = UvcDescriptors.ParseUnits(SampleDescriptors());
        Assert.Equal([(1, 2), (2, 5), (6, 6)], units.Select(u => ((int)u.Id, (int)u.Subtype)));
        Assert.Equal(ObsbotXu.Guid, units[^1].Guid);
        Assert.Equal("Extension Unit", units[^1].Kind);
    }

    [Fact]
    public void ToleratesTruncatedOrEmptyInput()
    {
        var full = SampleDescriptors();
        Assert.Empty(UvcDescriptors.ParseUnits([]));
        Assert.Null(UvcDescriptors.FindExtensionUnit(full.AsSpan(0, full.Length - 30), ObsbotXu.Guid));
        Assert.Empty(UvcDescriptors.ParseUnits([0, 0x24, 0x06]));
    }

    [Fact]
    public void OtherGuidIsNotMatched()
    {
        Assert.Null(UvcDescriptors.FindExtensionUnit(SampleDescriptors(), Guid.NewGuid()));
    }
}

public class WorkerThreadTests
{
    [Fact]
    public void RunsWorkOnItsOwnThreadAndPropagatesExceptions()
    {
        var worker = new WorkerThread("test-worker");
        int caller = Environment.CurrentManagedThreadId;
        int ran = worker.Invoke(() => Environment.CurrentManagedThreadId);
        Assert.NotEqual(caller, ran);
        Assert.Equal(ran, worker.Invoke(() => worker.Invoke(() => Environment.CurrentManagedThreadId)));
        Assert.Throws<InvalidOperationException>(() => worker.Invoke(() => throw new InvalidOperationException("x")));
    }

    [Fact]
    public void FactoryPicksBackendForThisOs()
    {
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux())
            return;
        var platform = CameraPlatform.Create();
        Assert.Contains(OperatingSystem.IsWindows() ? "Windows" : "Linux", platform.Name);
    }
}
