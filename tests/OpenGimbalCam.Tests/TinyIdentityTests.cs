using OpenGimbalCam.Core.Camera;
using Xunit;

namespace OpenGimbalCam.Tests;

public class TinyIdentityTests
{
    [Fact]
    public void Accepts_only_the_original_tiny()
    {
        Assert.True(TinyIdentity.IsMatch(0x3564, 0xFEF0));
        Assert.False(TinyIdentity.IsMatch(0x3564, 0xFEF1));
        Assert.False(TinyIdentity.IsMatch(0x3564, 0xFEF3));
        Assert.False(TinyIdentity.IsMatch(0x046D, 0xFEF0));
        Assert.False(TinyIdentity.IsMatch(null, null));
        Assert.False(TinyIdentity.IsMatch(0x3564, null));
    }

    [Theory]
    [InlineData(@"\\?\usb#vid_3564&pid_fef0&mi_00#7&1#{guid}", true)]
    [InlineData(@"\\?\USB#VID_3564&PID_FEF0&MI_00#7&1#{guid}", true)]
    [InlineData(@"\\?\usb#vid_3564&pid_fef3&mi_00#7&1#{guid}", false)]
    [InlineData(@"\\?\usb#vid_046d&pid_fef0&mi_00#7&1#{guid}", false)]
    [InlineData(@"\\?\usb#vid_3564&pid_fef", false)]
    [InlineData(@"\\?\usb#vid_zzzz&pid_fef0", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Parses_device_paths(string? path, bool expected) =>
        Assert.Equal(expected, TinyIdentity.IsMatchDevicePath(path));

    [Fact]
    public void CameraInfo_reflects_predicate()
    {
        Assert.True(new CameraInfo(0, "t", null, 0x3564, 0xFEF0).IsSupportedTiny);
        Assert.False(new CameraInfo(0, "t", null, 0x3564, 0xFEF3).IsSupportedTiny);
    }
}
