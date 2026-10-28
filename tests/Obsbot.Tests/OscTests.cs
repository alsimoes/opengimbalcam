using System.Net;
using System.Net.Sockets;
using Obsbot.Core.Camera;
using Obsbot.Core.Protocol;
using Obsbot.Osc.Bridge;
using Obsbot.Osc;
using Xunit;

namespace Obsbot.Tests;

public class OscMessageTests
{
    [Fact]
    public void EncodesLikeTheOscSpecExample()
    {
        // OSC 1.0 spec: "/oscillator/4/frequency" ,f 440.0
        var bytes = new OscMessage("/oscillator/4/frequency", 440.0f).Encode();
        Assert.Equal(
            "2F6F7363696C6C61746F722F342F6672657175656E637900" + "2C660000" + "43DC0000",
            Convert.ToHexString(bytes));
    }

    [Fact]
    public void RoundTripsAllTypes()
    {
        var m = new OscMessage("/OBSBOT/WebCam/General/DeviceInfo", 1, "Tiny", 2.5f, new byte[] { 1, 2, 3 }, 7L, 1.25, true, false, null);
        var d = OscMessage.Decode(m.Encode());
        Assert.Equal(m.Address, d.Address);
        Assert.Equal("isfbhdTFN", d.TypeTags);
        Assert.Equal(1, d.Args[0]);
        Assert.Equal("Tiny", d.Args[1]);
        Assert.Equal(2.5f, d.Args[2]);
        Assert.Equal(new byte[] { 1, 2, 3 }, d.Args[3]);
        Assert.Equal(7L, d.Args[4]);
        Assert.Equal(1.25, d.Args[5]);
        Assert.Equal(true, d.Args[6]);
        Assert.Equal(false, d.Args[7]);
        Assert.Null(d.Args[8]);
    }

    [Fact]
    public void FlattensBundles()
    {
        var a = new OscMessage("/a", 1).Encode();
        var b = new OscMessage("/b", "x").Encode();
        using var ms = new MemoryStream();
        ms.Write("#bundle\0"u8);
        ms.Write(new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 });
        foreach (var e in new[] { a, b })
        {
            ms.Write(new[] { (byte)0, (byte)0, (byte)(e.Length >> 8), (byte)e.Length });
            ms.Write(e);
        }
        var list = OscMessage.DecodePacket(ms.ToArray());
        Assert.Equal(["/a", "/b"], list.Select(m => m.Address));
    }

    [Fact]
    public void RejectsTruncatedPackets()
    {
        var bytes = new OscMessage("/x", 5).Encode();
        Assert.Throws<FormatException>(() => OscMessage.Decode(bytes.AsSpan(0, bytes.Length - 2)));
    }

    [Theory]
    [InlineData(OscFraming.LengthPrefixed)]
    [InlineData(OscFraming.Slip)]
    public async Task StreamFramingRoundTripsAndIsDetected(OscFraming framing)
    {
        // payload containing both SLIP special bytes
        var packets = new[] { new OscMessage("/s", new byte[] { 0xC0, 0xDB, 0x00 }).Encode(), new OscMessage("/t", 2).Encode() };
        using var ms = new MemoryStream();
        foreach (var p in packets)
            ms.Write(OscStreamFraming.Frame(p, framing));
        ms.Position = 0;

        var reader = new OscStreamFraming(ms);
        Assert.Equal(packets[0], await reader.ReadPacketAsync(default));
        Assert.Equal(framing, reader.Framing);
        Assert.Equal(packets[1], await reader.ReadPacketAsync(default));
        Assert.Null(await reader.ReadPacketAsync(default));
    }
}

public class BridgeTests
{
    private const string G = "/OBSBOT/WebCam/General/";

    private static (OscBridge Bridge, FakeHub Hub) Make(int cameras = 2)
    {
        var hub = new FakeHub(cameras);
        return (new OscBridge(hub, _ => { }), hub);
    }

    [Theory]
    [InlineData("SetGimbalUp", 50, 0, 0.5)]
    [InlineData("SetGimbalDown", 50, 0, -0.5)]
    [InlineData("SetGimbalLeft", 100, -1.0, 0)]
    [InlineData("SetGimbalRight", 250, 1.0, 0)] // clamped to 100 -> 1.0, as in OBSBOT_Main
    [InlineData("SetGimbalRight", 0, 0, 0)]
    public async Task GimbalDirectionsMatchObsbotCenter(string name, int speed, double pan, double tilt)
    {
        var (bridge, hub) = Make();
        await bridge.ExecuteAsync(new OscMessage(G + name, speed));
        Assert.Equal(($"pantilt {pan:0.##} {tilt:0.##}"), hub.Cams[0].Calls.Single());
    }

    [Fact]
    public async Task LeadingIntSelectsTheDevice()
    {
        var (bridge, hub) = Make();
        await bridge.ExecuteAsync(new OscMessage(G + "SetMirror", 1, 1));
        Assert.Empty(hub.Cams[0].Calls);
        Assert.Equal("mirror True", hub.Cams[1].Calls.Single());

        await bridge.ExecuteAsync(new OscMessage(G + "SelectDevice", 1));
        await bridge.ExecuteAsync(new OscMessage(G + "SetZoom", 30));
        Assert.Equal("zoom 30", hub.Cams[1].Calls.Last());
    }

    [Fact]
    public async Task InvalidDeviceIndexIsIgnored()
    {
        var (bridge, hub) = Make(1);
        Assert.Null(await bridge.ExecuteAsync(new OscMessage(G + "SetZoom", 3, 50)));
        Assert.Empty(hub.Cams[0].Calls);
    }

    [Fact]
    public async Task ConnectedIsAnswered()
    {
        var (bridge, hub) = Make();
        var reply = await bridge.ExecuteAsync(new OscMessage(G + "Connected", 1));
        Assert.Equal(new OscMessage(G + "ConnectedResp", 1).ToString(), reply!.ToString());
        Assert.Equal(1, hub.Refreshes);
    }

    [Fact]
    public async Task DeviceInfoHasObsbotLayout()
    {
        var (bridge, _) = Make(2);
        var reply = await bridge.ExecuteAsync(new OscMessage(G + "GetDeviceInfo", 0));
        Assert.Equal("isisisisiii", reply!.TypeTags);
        Assert.Equal([1, "Cam0", 1, "Cam1", 0, "", 0, ""], reply.Args.Take(8));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(5, 1)]
    [InlineData(15, 2)]
    [InlineData(40, -1)]
    public async Task ZoomInfoReportsViewLikeSendZoomInfo(int zoom, int view)
    {
        var (bridge, hub) = Make();
        hub.Cams[0].Zoom = zoom;
        var reply = await bridge.ExecuteAsync(new OscMessage(G + "GetZoomInfo", 0));
        Assert.Equal([zoom, view], reply!.Args);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(2, 2)]
    [InlineData(7, 3)]
    public async Task SetViewMapsToFovType(int value, int fov)
    {
        var (bridge, hub) = Make();
        await bridge.ExecuteAsync(new OscMessage(G + "SetView", value));
        Assert.Equal($"fov {fov}", hub.Cams[0].Calls.Single());
    }

    [Fact]
    public async Task KnownButUnsupportedAddressesDoNothing()
    {
        var (bridge, hub) = Make();
        Assert.Null(await bridge.ExecuteAsync(new OscMessage(G + "SetZoneTrack", 1)));
        Assert.Null(await bridge.ExecuteAsync(new OscMessage("/OBSBOT/Nope", 0)));
        Assert.Empty(hub.Cams[0].Calls);
    }

    [Fact]
    public async Task OnlyRoutesWithoutEffectOnTheTinyRemainUnimplemented()
    {
        var (bridge, _) = Make();
        string[] missing = bridge.Routes.Where(r => r.Handler is null).Select(r => r.Address[OscBridge.Prefix.Length..]).Order().ToArray();
        Assert.Equal(
            ["General/SetAutoFocusMode", "General/SetGimWhiteBalanceShift", "General/SetRBGain", "General/SetZoneTrack", "General/TriggerZonePreset",
             "Meet/GetAutoFramingInfo", "Meet/GetPresetPositionInfo", "Meet/GetVirtualBackgroundInfo", "Meet/SetAutoFraming", "Meet/SetStandardMode",
             "Meet/SetVirtualBackground", "Tiny/SetTrackingSpeed"],
            missing);
        await Task.CompletedTask;
    }

    [Theory]
    [InlineData("SetAntiFlicker", 1, "raw PowerlineFrequency 1")]
    [InlineData("SetAutoExposureMode", 5, "cmd face-ae 1")]
    [InlineData("SetExposureCompensate", -10, "raw BacklightCompensation 6")]
    [InlineData("SetHDR", 2, "cmd hdr 1")]
    [InlineData("SetISO", 400, "raw Gain 4")]
    [InlineData("SetShutterSpeed", 100, "shutter 100")]
    [InlineData("WakeSleep", 0, "cameraSetDevRunStatusR: sender=1 set=1 id=0x02 payload=0103")]
    public async Task GeneralRoutesReachTheCamera(string route, int value, string call)
    {
        var (bridge, hub) = Make();
        await bridge.ExecuteAsync(new OscMessage(G + route, value));
        Assert.Equal(call, hub.Cams[0].Calls.Single());
    }

    [Theory]
    [InlineData("SetAntiFlicker")]
    [InlineData("SetAutoExposureMode")]
    [InlineData("SetExposureCompensate")]
    public async Task AutoExposureOnlyRoutesAreIgnoredInManualExposure(string route)
    {
        var (bridge, hub) = Make();
        hub.Cams[0].AutoExposure = false;
        await bridge.ExecuteAsync(new OscMessage(G + route, 1));
        Assert.Empty(hub.Cams[0].Calls);
    }

    [Theory]
    [InlineData("SetAiMode", 1, "cmd ai-mode 2 0")]
    [InlineData("SetAiMode", 9, "cmd ai-mode 1 0")]
    [InlineData("SetTrackingMode", 0, "aiSetTrackingModeR: sender=3 set=3 id=0x11 payload=01")]
    [InlineData("SetTrackingMode", 1, "aiSetTrackingModeR: sender=3 set=3 id=0x11 payload=00")]
    [InlineData("ToggleAILock", 1, "aiSetTargetSelectR: sender=3 set=3 id=0x0F payload=00")]
    [InlineData("ToggleAILock", 0, "aiSetTargetSelectR: sender=3 set=3 id=0x0E payload=")]
    public async Task TinyRoutesFollowObsbotCenter(string route, int value, string call)
    {
        var (bridge, hub) = Make();
        await bridge.ExecuteAsync(new OscMessage(OscBridge.Prefix + "Tiny/" + route, value));
        Assert.Equal(call, hub.Cams[0].Calls.Single());
    }

    [Fact]
    public async Task GimMotorDegreeSendsSpeedsThenPositions()
    {
        var (bridge, hub) = Make();
        await bridge.ExecuteAsync(new OscMessage(G + "SetGimMotorDegree", 200, 30, -70));
        // speed clamped to 90, pitch to -59: 9000 x3, roll 0, pitch -5900, yaw 3000 (int16 LE)
        Assert.Equal("gimbalSetSpeedPositionR: sender=2 set=2 id=0x03 payload=2823282328230000F4E8B80B", hub.Cams[0].Calls.Single());
    }

    [Fact]
    public async Task GimMotorDegreeExAcceptsASerialNumber()
    {
        var (bridge, hub) = Make();
        await bridge.ExecuteAsync(new OscMessage(G + "SetGimMotorDegreeEx", "SN123", 10f, 0f, 0f));
        Assert.StartsWith("gimbalSetSpeedPositionR:", hub.Cams[0].Calls.Single());
    }

    [Fact]
    public async Task WakeSleepIsReportedInDeviceInfo()
    {
        var (bridge, _) = Make();
        await bridge.ExecuteAsync(new OscMessage(G + "WakeSleep", 0));
        var info = await bridge.ExecuteAsync(new OscMessage(G + "GetDeviceInfo", 0));
        Assert.Equal(0, info!.Args[9]);
    }

    [Fact]
    public async Task PcRoutesUseTheMediaHandler()
    {
        var hub = new FakeHub(1);
        var media = new FakeMedia();
        var bridge = new OscBridge(hub, _ => { }, media);
        await bridge.ExecuteAsync(new OscMessage(G + "PCSnapshot", 1));
        await bridge.ExecuteAsync(new OscMessage(G + "SetPCRecording", 1));
        Assert.Equal(["snapshot 0", "record 0 True False"], media.Calls);
    }

    [Theory]
    [InlineData(-30, 0)]
    [InlineData(0, 9)]
    [InlineData(7, 11)]
    [InlineData(30, 18)]
    [InlineData(99, 18)]
    public void EvIndexFollowsTheThirdStopTable(int evTimesTen, int index) =>
        Assert.Equal(index, OscBridge.EvIndex(evTimesTen));

    [Theory]
    [InlineData(-13, -2, 100, -7)]   // DirectShow: log2(1/100 s)
    [InlineData(-13, -2, 8000, -13)]
    [InlineData(1, 5000, 100, 100)]  // V4L2: 100 µs units
    public void ShutterSpeedMapsToThePlatformUnits(int min, int max, int denominator, int expected) =>
        Assert.Equal(expected, ObsbotCamera.ExposureValue(new ControlRange(min, max, 1, min, default), denominator));

    [Fact]
    public void RouteTableCoversEveryWebcamAddressOfObsbotMain()
    {
        // every /OBSBOT/WebCam/* path registered by OBSBOT_Main.exe 2.0.14 (RVA 0x10E590)
        string[] registered =
        [
            "General/Connected", "General/Disconnected", "General/SelectDevice", "General/WakeSleep", "General/ResetGimbal",
            "General/SetZoom", "General/SetZoomSpeed", "General/SetZoomMax", "General/SetZoomMin", "General/SetGimbalUp",
            "General/SetGimbalDown", "General/SetGimbalLeft", "General/SetGimbalRight", "General/SetView", "General/SetGimMotorDegree",
            "General/SetGimMotorDegreeEx", "General/SetMirror", "General/SetPCRecording", "General/SetHighfpsRecording",
            "Tiny/ToggleAILock", "Tiny/TriggerPreset", "Meet/SetVirtualBackground", "Meet/SetAutoFraming", "Meet/SetStandardMode",
            "Tiny/SetAiMode", "Tiny/SetTrackingMode", "Tiny/SetTrackingSpeed", "General/SetZoneTrack", "General/TriggerZonePreset",
            "General/SetPTZValue", "General/SetHDR", "General/SetAutoFocus", "General/SetAutoFocusMode", "General/SetManualFocus",
            "General/SetAutoExposure", "General/SetAutoExposureMode", "General/SetExposureCompensate", "General/SetShutterSpeed",
            "General/SetISO", "General/SetAntiFlicker", "General/SetAutoWhiteBalance", "General/SetGimWhiteBalanceShift",
            "General/SetRBGain", "General/SetColorTemperature", "General/SetContrast", "General/SetSaturation", "General/SetSharp",
            "General/SetHue", "General/PCSnapshot", "General/GetGimbalPosInfo", "General/GetDeviceInfo", "Tiny/GetPresetPositionInfo",
            "Meet/GetPresetPositionInfo", "Tiny/GetAiTrackingInfo", "Meet/GetVirtualBackgroundInfo", "Meet/GetAutoFramingInfo",
            "General/GetZoomInfo",
        ];
        var (bridge, _) = Make();
        var known = bridge.Routes.Select(r => r.Address).ToHashSet();
        Assert.All(registered, r => Assert.Contains(OscBridge.Prefix + r, known));
    }

    [Theory]
    [InlineData(0.5, -96, 80)]   // Tiny: PanRelative max 0xA0 reported as -96
    [InlineData(-1.0, 120, -120)]
    [InlineData(0.333, 120, 39)] // truncation, like cvttsd2si
    [InlineData(2.0, 0, 100)]    // no range: fallback 100
    public void SpeedScalingFollowsLibdev(double v, int max, int expected) =>
        Assert.Equal(expected, ObsbotCamera.ScaleSpeed(v, max));

    [Theory]
    [InlineData(0, 100, 1, 50, 50)]
    [InlineData(2800, 6500, 100, 50, 4700)]
    [InlineData(-13, -2, 1, 100, -2)]
    [InlineData(0, 100, 1, 150, 100)]
    public void PercentMapsIntoUvcRange(int min, int max, int step, int percent, int expected) =>
        Assert.Equal(expected, ObsbotCamera.FromPercent(new ControlRange(min, max, step, min, default), percent));
}

public class ServerTests
{
    [Fact]
    public async Task UdpRepliesToTheSender()
    {
        var bridge = new OscBridge(new FakeHub(1), _ => { });
        await using var server = new OscServer(OscTransport.Udp, new IPEndPoint(IPAddress.Loopback, 0), bridge.HandleAsync, _ => { });
        server.Start();

        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        await client.SendAsync(new OscMessage("/OBSBOT/WebCam/General/Connected", 1).Encode(), server.LocalEndPoint);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var r = await client.ReceiveAsync(cts.Token);
        Assert.Equal("/OBSBOT/WebCam/General/ConnectedResp", OscMessage.Decode(r.Buffer).Address);
    }

    [Theory]
    [InlineData(OscFraming.LengthPrefixed)]
    [InlineData(OscFraming.Slip)]
    public async Task TcpRepliesWithTheClientsFraming(OscFraming framing)
    {
        var bridge = new OscBridge(new FakeHub(1), _ => { });
        await using var server = new OscServer(OscTransport.Tcp, new IPEndPoint(IPAddress.Loopback, 0), bridge.HandleAsync, _ => { });
        server.Start();

        using var client = new TcpClient();
        await client.ConnectAsync(server.LocalEndPoint);
        var stream = new OscStreamFraming(client.GetStream(), framing);
        await stream.WritePacketAsync(new OscMessage("/OBSBOT/WebCam/General/GetZoomInfo", 0).Encode(), default);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var reply = OscMessage.Decode((await stream.ReadPacketAsync(cts.Token))!);
        Assert.Equal("/OBSBOT/WebCam/General/ZoomInfo", reply.Address);
        Assert.Equal([0, 0], reply.Args);
    }
}

internal sealed class FakeHub(int count) : ICameraHub
{
    public List<FakeCamera> Cams { get; } = Enumerable.Range(0, count).Select(i => new FakeCamera($"Cam{i}")).ToList();
    public int Refreshes { get; private set; }
    public IReadOnlyList<ICamera> Cameras => Cams;

    public void Refresh() => Refreshes++;
}

internal sealed class FakeCamera(string name) : ICamera
{
    public List<string> Calls { get; } = [];
    public int Zoom { get; set; }
    public string Name => name;
    public int ProductCode => 0;
    public bool AutoExposure { get; set; } = true;

    public void SetPanTiltSpeed(double pan, double tilt) => Calls.Add($"pantilt {pan:0.##} {tilt:0.##}");
    public void ResetPanTilt() => Calls.Add("reset");
    public (int Pan, int Tilt)? GetPanTilt() => (0, 0);
    public int? GetZoomPercent() => Zoom;
    public void SetZoomPercent(int percent) => Calls.Add($"zoom {percent}");
    public void SetFov(int fovType) => Calls.Add($"fov {fovType}");
    public void SetMirror(bool on) => Calls.Add($"mirror {on}");
    public void TriggerPreset(int presetId) => Calls.Add($"preset {presetId}");
    public void SetAuto(UvcControl control, bool auto) => Calls.Add($"auto {control} {auto}");
    public void SetPercent(UvcControl control, int percent) => Calls.Add($"pct {control} {percent}");
    public void SetRaw(UvcControl control, int value) => Calls.Add($"raw {control} {value}");
    public bool? IsAuto(UvcControl control) => control == UvcControl.Exposure ? AutoExposure : null;
    public void SetShutterSpeed(int denominator) => Calls.Add($"shutter {denominator}");
    public void SendCommand(string name, params int[] args) => Calls.Add($"cmd {name} {string.Join(' ', args)}");
    public void SendMessage(RemoRequest request) => Calls.Add(request.ToString());

    public void Dispose()
    {
    }
}

internal sealed class FakeMedia : IPcMedia
{
    public List<string> Calls { get; } = [];

    public void Snapshot(int device) => Calls.Add($"snapshot {device}");
    public void SetRecording(int device, bool on, bool highFps) => Calls.Add($"record {device} {on} {highFps}");
}
