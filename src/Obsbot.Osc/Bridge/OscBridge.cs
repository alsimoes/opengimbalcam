using Obsbot.Osc;

namespace Obsbot.Osc.Bridge;

/// <summary>
/// One OSC address of OBSBOT Center's protocol.
/// </summary>
/// <param name="Address">OSC path, as registered in OBSBOT_Main.exe (RVA 0x10E590).</param>
/// <param name="Values">Value arguments; a message with one extra leading int addresses a device by index.</param>
/// <param name="Handler">null when the address is known but not implemented (see <paramref name="Evidence"/>).</param>
/// <param name="Evidence">What OBSBOT Center does for this address: handler RVA and libdev calls.</param>
public sealed record OscRoute(string Address, int Values, Func<ICamera, OscMessage, IReadOnlyList<int>, OscMessage?>? Handler, string Evidence);

/// <summary>
/// Implements OBSBOT Center's OSC protocol on top of <see cref="ICameraHub"/>.
/// Addressing rule taken from every handler (e.g. SetGimbalUp, RVA 0x111980):
/// with exactly the value arguments the message targets the selected device;
/// with one more int in front, that int is the device index (0..3).
/// </summary>
public sealed class OscBridge
{
    public const string Prefix = "/OBSBOT/WebCam/";

    private readonly ICameraHub _hub;
    private readonly Action<string> _log;
    private readonly SemaphoreSlim _cameraLock = new(1, 1);
    private readonly HashSet<string> _warned = [];
    private readonly Dictionary<string, OscRoute> _routes;

    public OscBridge(ICameraHub hub, Action<string> log)
    {
        _hub = hub;
        _log = log;
        _routes = BuildRoutes().ToDictionary(r => r.Address, StringComparer.Ordinal);
    }

    /// <summary>Index of the device that single-value messages act on (OSC SelectDevice).</summary>
    public int SelectedDevice { get; private set; }

    public IReadOnlyCollection<OscRoute> Routes => _routes.Values;

    public async Task HandleAsync(OscMessage message, IOscPeer peer, CancellationToken ct)
    {
        var reply = await ExecuteAsync(message, ct);
        if (reply is not null)
        {
            await peer.SendAsync(reply, ct);
            _log($"{peer.Description} <- {reply}");
        }
    }

    /// <summary>Runs one message and returns the reply OBSBOT Center would send, if any.</summary>
    public async Task<OscMessage?> ExecuteAsync(OscMessage message, CancellationToken ct = default)
    {
        _log($"-> {message}");
        if (!_routes.TryGetValue(message.Address, out var route))
        {
            Warn(message.Address, "endereço desconhecido (não existe no OBSBOT Center 2.0.14)");
            return null;
        }
        if (route.Handler is null)
        {
            Warn(message.Address, $"não implementado: {route.Evidence}");
            return null;
        }

        int argc = message.Args.Count;
        int device;
        int first;
        if (argc == route.Values)
        {
            device = SelectedDevice;
            first = 0;
        }
        else if (argc == route.Values + 1)
        {
            device = message.Int(0);
            first = 1;
        }
        else
        {
            _log($"{message.Address}: esperava {route.Values} argumento(s) (+1 opcional de índice), recebeu {argc}");
            return null;
        }
        var values = Enumerable.Range(first, route.Values).Select(message.Int).ToArray();

        await _cameraLock.WaitAsync(ct);
        try
        {
            if (route.Values == 0 || IsDeviceAgnostic(route.Address))
                return route.Handler(null!, message, values);
            var cameras = _hub.Cameras;
            if (device < 0 || device >= cameras.Count)
            {
                // OBSBOT logs "OSC - <name> device index: %d invalid" and ignores the message.
                _log($"{message.Address}: índice de dispositivo {device} inválido ({cameras.Count} câmera(s))");
                return null;
            }
            return route.Handler(cameras[device], message, values);
        }
        finally
        {
            _cameraLock.Release();
        }
    }

    private static bool IsDeviceAgnostic(string address) =>
        address is Prefix + "General/Connected" or Prefix + "General/Disconnected"
            or Prefix + "General/SelectDevice" or Prefix + "General/GetDeviceInfo";

    private void Warn(string address, string text)
    {
        bool first;
        lock (_warned)
            first = _warned.Add(address);
        if (first)
            _log($"{address}: {text}");
    }

    private IEnumerable<OscRoute> BuildRoutes()
    {
        const string G = Prefix + "General/", T = Prefix + "Tiny/", M = Prefix + "Meet/";
        const string Msg = "usa o canal de mensagens 0xAA da libdev, cujas tabelas de IDs não foram totalmente recuperadas";

        // --- sessão ---------------------------------------------------------
        yield return new(G + "Connected", 1, (_, _, _) => { _hub.Refresh(); return new OscMessage(G + "ConnectedResp", 1); },
            "handler 0x135A20; resposta sendConnectedResp (0x1214B0) = ConnectedResp ,i 1");
        yield return new(G + "Disconnected", 1, (_, _, _) => null, "handler 0x120EB0 (só log)");
        yield return new(G + "SelectDevice", 1, (_, _, v) => { SelectedDevice = Math.Clamp(v[0], 0, ObsbotCameraHub.MaxDevices - 1); return null; },
            "handler 0x1358F0: troca o dispositivo selecionado");
        yield return new(G + "GetDeviceInfo", 1, (_, _, _) => DeviceInfo(), "sendDevInfo (0x1215A0): ,isisisisiii");

        // --- gimbal ---------------------------------------------------------
        // speed s in 0..100 -> v = min(s * 0.01, 1); Up = tilt +v, Down = -v, Left = pan -v, Right = +v.
        yield return new(G + "SetGimbalUp", 1, (c, _, v) => Gimbal(c, 0, +Speed(v[0])), "0x111980 -> cameraSetPanTiltRelative(0, +v)");
        yield return new(G + "SetGimbalDown", 1, (c, _, v) => Gimbal(c, 0, -Speed(v[0])), "0x111E40 -> cameraSetPanTiltRelative(0, -v)");
        yield return new(G + "SetGimbalLeft", 1, (c, _, v) => Gimbal(c, -Speed(v[0]), 0), "0x112300 -> cameraSetPanTiltRelative(-v, 0)");
        yield return new(G + "SetGimbalRight", 1, (c, _, v) => Gimbal(c, +Speed(v[0]), 0), "0x1127F0 -> cameraSetPanTiltRelative(+v, 0)");
        yield return new(G + "ResetGimbal", 1, (c, _, _) => { c.ResetPanTilt(); return null; },
            "0x1107B0 -> cameraSetPanTiltAbsolute(0, 0) (ou aiTrgGimbalBootPosR em modelos do canal 0xAA)");
        yield return new(G + "GetGimbalPosInfo", 1, (c, _, _) => c.GetPanTilt() is { } pt ? new OscMessage(G + "GetGimbalPosInfoResp", pt.Pan, pt.Tilt) : null,
            "sendGimbalPosInfo (0x1218A0): ,ii a partir de gimbalGetAttitudeInfoR; aqui: Pan/Tilt UVC em graus");

        // --- zoom e campo de visão ---------------------------------------------
        yield return new(G + "SetZoom", 1, (c, _, v) => { c.SetZoomPercent(v[0]); return null; },
            "0x110B10 -> cameraSetZoomAbsoluteR(1 + z*0.01); aqui: z % da faixa UVC Zoom");
        yield return new(G + "SetZoomMax", 1, (c, _, _) => { c.SetZoomPercent(100); return null; }, "0x111260 -> cameraSetZoomAbsoluteR(2.0)");
        yield return new(G + "SetZoomMin", 1, (c, _, _) => { c.SetZoomPercent(0); return null; }, "0x1115F0 -> cameraSetZoomAbsoluteR(1.0)");
        yield return new(G + "SetZoomSpeed", 2, (c, _, v) => { c.SetZoomPercent(v[0]); return null; },
            "0x110F00 -> cameraSetZoomWithSpeedAbsoluteR(zoom, velocidade); aqui a velocidade é ignorada");
        yield return new(G + "GetZoomInfo", 1, (c, _, _) => ZoomInfo(c), "sendZoomInfo (0x122BA0): ,ii zoom 0..100 e visão 0/1/2 (zoom 0/5/15) ou -1");
        yield return new(G + "SetView", 1, (c, _, v) => { c.SetFov(v[0] is >= 0 and <= 2 ? v[0] : 3); return null; },
            "0x112CE0 -> cameraSetFovU(0=86°, 1=78°, 2=65°, outro=3): XU 6 cmd 04");
        yield return new(G + "SetMirror", 1, (c, _, v) => { c.SetMirror(v[0] == 1); return null; },
            "0x113760 -> cameraSetImageFlipHorizonU(v == 1): XU 6 cmd 14");

        // --- imagem (UVC) -------------------------------------------------------
        yield return new(G + "SetAutoFocus", 1, (c, _, v) => { c.SetAuto(UvcControl.Focus, v[0] != 0); return null; }, "0x11A6F0 -> cameraSetFocusAbsolute (UVC)");
        yield return new(G + "SetManualFocus", 1, (c, _, v) => { c.SetPercent(UvcControl.Focus, v[0]); return null; }, "0x11AE40 -> cameraSetFocusAbsolute (UVC)");
        yield return new(G + "SetAutoExposure", 1, (c, _, v) => { c.SetAuto(UvcControl.Exposure, v[0] != 0); return null; },
            "0x11B220 -> cameraGet/SetExposureAbsolute (UVC)");
        yield return new(G + "SetAutoWhiteBalance", 1, (c, _, v) => { c.SetAuto(UvcControl.WhiteBalance, v[0] != 0); return null; },
            "0x11CB80 -> cameraSetWhiteBalanceR; aqui: White Balance UVC auto/manual");
        yield return new(G + "SetColorTemperature", 1, (c, _, v) => { c.SetRaw(UvcControl.WhiteBalance, v[0]); return null; },
            "0x11D850 -> cameraSetWhiteBalanceR/Setting; aqui: White Balance UVC em kelvin");
        yield return new(G + "SetContrast", 1, (c, _, v) => { c.SetPercent(UvcControl.Contrast, v[0]); return null; }, "0x11DDC0 -> cameraSetImageContrastR; aqui: Contrast UVC");
        yield return new(G + "SetSaturation", 1, (c, _, v) => { c.SetPercent(UvcControl.Saturation, v[0]); return null; }, "0x11E140 -> cameraSetImageSaturationR; aqui: Saturation UVC");
        yield return new(G + "SetSharp", 1, (c, _, v) => { c.SetPercent(UvcControl.Sharpness, v[0]); return null; }, "0x11E4C0 -> cameraSetImageSharpR; aqui: Sharpness UVC");
        yield return new(G + "SetHue", 1, (c, _, v) => { c.SetPercent(UvcControl.Hue, v[0]); return null; }, "0x11E840 -> cameraSetImageHueR; aqui: Hue UVC");

        // --- Tiny ---------------------------------------------------------------
        yield return new(T + "TriggerPreset", 1, (c, _, v) => { c.TriggerPreset(v[0]); return null; },
            "0x1140D0/0x115150 -> cameraTriCameraPreset ou aiTrgGimbalPresetR; aqui: XU 7 (cameraTrgGimbalPresetU, modelos antigos)");

        // --- conhecidos, não implementados -----------------------------------------
        foreach (var (addr, values, why) in new (string, int, string)[]
        {
            (G + "WakeSleep", 1, "0x110450 -> cameraSetDevRunStatusR(0 -> 3, outro -> 1): " + Msg),
            (G + "SetGimMotorDegree", 3, "0x113110 -> gimbalSetSpeedPositionR: " + Msg),
            (G + "SetGimMotorDegreeEx", 4, "0x113420 -> gimbalSetSpeedPositionR: " + Msg),
            (G + "SetPTZValue", 3, "0x119C90 -> gimbalSetSpeedPositionR/aiSetGimbalMotorAngleR: " + Msg),
            (G + "SetHDR", 1, "0x11A3A0 -> cameraSetWdrR: " + Msg),
            (G + "SetAutoFocusMode", 1, "0x11AA80 -> cameraSetFaceFocusR: " + Msg),
            (G + "SetAutoExposureMode", 1, "0x11B5A0 -> cameraSetFaceAER: " + Msg),
            (G + "SetExposureCompensate", 1, "0x11B930 -> cameraSetPAEEvBiasR: " + Msg),
            (G + "SetShutterSpeed", 1, "0x11BDC0: mapeamento valor -> exposição UVC não recuperado"),
            (G + "SetISO", 1, "0x11C3E0 -> cameraSetMAEIsoR: " + Msg),
            (G + "SetAntiFlicker", 1, "0x11C7E0 -> cameraSetAntiFlickR: " + Msg),
            (G + "SetGimWhiteBalanceShift", 2, "0x11CF00 -> cameraSetWhiteBalanceSetting: " + Msg),
            (G + "SetRBGain", 2, "0x11D3E0 -> cameraSetWhiteBalanceSetting: " + Msg),
            (G + "SetZoneTrack", 1, "0x119620 -> aiSetZoneTrackStateR: " + Msg),
            (G + "TriggerZonePreset", 1, "0x119970 -> aiTrgZonePresetR: " + Msg),
            (G + "SetPCRecording", 1, "0x1353D0: gravação no PC, feita pelo processo de mídia (OBSBOT_Center.exe)"),
            (G + "SetHighfpsRecording", 1, "0x135B80: gravação no PC, feita pelo processo de mídia"),
            (G + "PCSnapshot", 1, "0x1349F0: captura no PC, feita pelo processo de mídia"),
            (T + "ToggleAILock", 1, "0x113B10 -> aiSetTargetSelectR / cameraSetAiModeU, decidido pelo estado da UI"),
            (T + "SetAiMode", 1, "0x116830: clica botões da UI conforme o modelo; mapeamento valor -> cameraSetAiModeU não recuperado"),
            (T + "SetTrackingMode", 1, "0x117190 -> aiSetTrackingModeR: " + Msg),
            (T + "SetTrackingSpeed", 1, "0x117550 -> aiSetControlParaR: " + Msg),
            (T + "GetPresetPositionInfo", 1, "0x11FA10: lê presets do estado da UI do OBSBOT Center"),
            (T + "GetAiTrackingInfo", 1, "0x11FF50: lê o estado da UI do OBSBOT Center"),
            (M + "SetVirtualBackground", 1, "0x1156C0 -> cameraSetMediaModeU + cameraSetBgModeU, só modelos Meet"),
            (M + "SetAutoFraming", 1, "0x115E10 -> cameraSetAutoFramingModeU, só modelos Meet"),
            (M + "SetStandardMode", 1, "0x116420 -> cameraSetMediaModeU, só modelos Meet"),
            (M + "GetPresetPositionInfo", 1, "0x11FCB0: estado da UI"),
            (M + "GetVirtualBackgroundInfo", 1, "0x1201F0: estado da UI"),
            (M + "GetAutoFramingInfo", 1, "0x120490: estado da UI"),
        })
            yield return new(addr, values, null, why);
    }

    private static double Speed(int s) => Math.Min(Math.Clamp(s, 0, 100) * 0.01, 1.0);

    private static OscMessage? Gimbal(ICamera c, double pan, double tilt)
    {
        c.SetPanTiltSpeed(pan, tilt);
        return null;
    }

    private static OscMessage? ZoomInfo(ICamera c)
    {
        if (c.GetZoomPercent() is not { } z)
            return null;
        // sendZoomInfo: view index derived from the slider position, as the TouchOSC sample expects.
        int view = z switch { 0 => 0, 5 => 1, 15 => 2, _ => -1 };
        return new OscMessage(Prefix + "General/ZoomInfo", z, view);
    }

    private OscMessage DeviceInfo()
    {
        // ,isisisisiii: 4 x (connected, name), selected index, run status, product code of the selected device.
        var cams = _hub.Cameras;
        var args = new List<object?>();
        for (int i = 0; i < ObsbotCameraHub.MaxDevices; i++)
        {
            args.Add(i < cams.Count ? 1 : 0);
            args.Add(i < cams.Count ? cams[i].Name : "");
        }
        args.Add(SelectedDevice);
        args.Add(1); // awake: the run status cannot be read without the 0xAA channel
        args.Add(SelectedDevice < cams.Count ? cams[SelectedDevice].ProductCode : 0);
        return new OscMessage(Prefix + "General/DeviceInfo", args);
    }
}
