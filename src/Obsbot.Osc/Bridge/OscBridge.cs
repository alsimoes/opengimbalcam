using Obsbot.Core.Protocol;
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
    private readonly Dictionary<ICamera, DeviceState> _state = new(ReferenceEqualityComparer.Instance);
    private readonly IPcMedia? _media;

    /// <param name="media">Snapshot/recording on the PC; null where there is no video (the CLI server).</param>
    public OscBridge(ICameraHub hub, Action<string> log, IPcMedia? media = null)
    {
        _hub = hub;
        _log = log;
        _media = media;
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
            // SetGimMotorDegreeEx names the device by serial number, which UVC does not expose.
            device = message.Args[0] is string ? SelectedDevice : message.Int(0);
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
        catch (Exception ex) when (ex is NotSupportedException or Obsbot.Core.Camera.XuException)
        {
            _log($"{message.Address}: {ex.Message}");
            return null;
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

        // --- exposição e imagem (UVC + XU 6) ------------------------------------------
        // OBSBOT_Main only applies these three while exposure is automatic (cameraGetExposureAbsolute).
        yield return new(G + "SetAntiFlicker", 1, (c, m, v) => WhenAutoExposure(c, m, () =>
            {
                if (v[0] is >= 0 and <= 2)
                    c.SetRaw(UvcControl.PowerlineFrequency, v[0]);
            }),
            "0x11C7E0 -> cameraSetAntiFlickR(0..2), só com exposição automática: na UVC, IAMVideoProcAmp PowerlineFrequency (0 = desligado, 1 = 50 Hz, 2 = 60 Hz)");
        yield return new(G + "SetAutoExposureMode", 1, (c, m, v) => WhenAutoExposure(c, m, () => c.SendCommand("face-ae", Math.Clamp(v[0], 0, 1))),
            "0x11B5A0 -> cameraSetFaceAER(0/1), só com exposição automática: XU 6 cmd 03");
        yield return new(G + "SetExposureCompensate", 1, (c, m, v) => WhenAutoExposure(c, m, () => c.SetRaw(UvcControl.BacklightCompensation, EvIndex(v[0]))),
            "0x11B930 -> cameraSetPAEEvBiasR(índice), só com exposição automática: valor/10 = EV, tabela de 19 passos de 1/3 EV (-3..+3, RVA 0x8C600) -> BacklightCompensation 0..18");
        yield return new(G + "SetHDR", 1, (c, _, v) => { RequireNotTypes(c, "SetHDR", 4, 0x0B, 0x10, 0x16); c.SendCommand("hdr", v[0] != 0 ? 1 : 0); return null; },
            "0x11A3A0 -> cameraSetWdrR: XU 6 cmd 01 (tipos 4/11/16/22 usam a mensagem set 1 id 0x14 do protocolo V3)");
        yield return new(G + "SetISO", 1, (c, _, v) => { RequireNotTypes(c, "SetISO", 0x12); c.SetRaw(UvcControl.Gain, (int)Math.Round(v[0] / 100.0)); return null; },
            "0x11C3E0 -> cameraSetMAEIsoR(ISO/100): tabela (i, i*100), i = 1..128 (RVA 0x8C910) -> IAMVideoProcAmp Gain");
        yield return new(G + "SetShutterSpeed", 1, (c, _, v) => { c.SetShutterSpeed(v[0]); return null; },
            "0x11BDC0 -> cameraSetExposureAbsolute(manual): valor = denominador (1/N s, tabela 6400..2 em RVA 0x8CDD0); aqui: log2(1/N) no DirectShow, 10000/N no V4L2");
        yield return new(G + "SetAutoFocusMode", 1, (c, _, v) =>
            {
                RequireTypes(c, "SetAutoFocusMode", 0, 1, 2, 3, 5, 6, 10, 12, 13, 18, 19, 25);
                c.SendMessage(RemoV1.FaceFocus(v[0] == 1));
                return null;
            },
            "0x11AA80 -> cameraSetFaceFocusR(v == 1): mensagem 0xAA set 1 id 3 (V1: 0x3D2)");

        // --- estado e gimbal pelo canal 0xAA ---------------------------------------
        yield return new(G + "WakeSleep", 1, (c, _, v) =>
            {
                c.SendMessage(RemoV1.DevRunStatus(v[0] == 0 ? (byte)3 : (byte)1));
                StateOf(c).Awake = v[0] != 0;
                return null;
            },
            "0x110450 -> cameraSetDevRunStatusR(0 -> 3 dormir, outro -> 1 acordar): mensagem 0xAA set 1 id 2 (V1: 0x3C2), payload [1, estado]");
        yield return new(G + "SetGimMotorDegree", 3, (c, _, v) => GimbalTo(c, Math.Clamp(v[0], 0, 90), v[1], v[2]),
            "0x113110 -> gimbalSetSpeedPositionR(0, pitch, yaw, v, v, v) com (velocidade 0..90, pan ±129, pitch ±59): mensagem 0xAA set 2 id 3 (V1: 0x006)");
        yield return new(G + "SetGimMotorDegreeEx", 3, (c, _, v) => GimbalTo(c, Math.Clamp(v[0], 0, 90), v[1], v[2]),
            "0x113420 -> gimbalSetSpeedPositionR; o argumento extra da frente identifica o aparelho (número de série) e os valores são float");
        yield return new(G + "SetPTZValue", 3, (c, _, v) => PtzValue(c, v[0], v[1], v[2]),
            "0x119C90 -> aiSetTargetSelectR(false) + gimbalSetSpeedPositionR (tempo = maior deslocamento / 20 °/s, até 3 s) + cameraSetZoomAbsoluteR(1 + z/100), com (pan ±130, pitch ±60, zoom 0..100)");

        // --- IA (Tiny) -------------------------------------------------------------
        yield return new(T + "SetAiMode", 1, (c, _, v) => AiMode(c, v[0]),
            "0x116830 -> cameraSetAiModeU(modo, submodo): XU 6 cmd 16; tabela 0..9 em RVA 0x117164 (tipos 0..3 e 12) ou sequência própria (tipos 18/19)");
        yield return new(T + "SetTrackingMode", 1, (c, _, v) => TrackingMode(c, v[0]),
            "0x117190 -> aiSetTrackingModeR(1 -> 0, 2 -> 2, 0 -> 1 só nos tipos 0/1): mensagem 0xAA set 3 id 0x11 (V1: 0x091)");
        yield return new(T + "ToggleAILock", 1, (c, _, v) => AiLock(c, v[0] == 1),
            "0x113B10 -> tipos 0/1: aiSetTargetSelectR(v == 1), mensagem 0xAA set 3 id 0x0F/0x0E (V1: 0x067/0x066); tipos 2/3/12/18/19: cameraSetAiModeU");
        yield return new(T + "GetAiTrackingInfo", 1, (c, _, _) => new OscMessage(T + "AiTrackingInfo", StateOf(c).AiMode != 0 ? 1 : 0),
            "0x11FF50 -> sendAiTrackingInfo (0x1220B0): ,i; aqui: 1 se o último SetAiMode/ToggleAILock ligou a IA");
        yield return new(T + "GetPresetPositionInfo", 1, (_, _, _) => PresetInfo(T),
            "0x11FA10 -> sendPresetPositionInfo (0x121E60): ,isisis (3 x id, nome); os nomes vêm da UI do OBSBOT Center, aqui são genéricos");

        // --- Meet ------------------------------------------------------------------
        yield return new(M + "SetVirtualBackground", 1, (c, _, v) => VirtualBackground(c, v[0]),
            "0x1156C0 -> 0: cameraSetMediaModeU(0); 1: modo 1 + cameraSetBgModeU(0x12); 2: modo 1 + fundo 1; 3: modo 1 + fundo 0x11");
        yield return new(M + "SetAutoFraming", 1, (c, _, v) => AutoFraming(c, v[0]),
            "0x115E10 -> 0: cameraSetMediaModeU(0); outro: cameraSetMediaModeU(2) + cameraSetAutoFramingModeU conforme o estado da câmera (aqui o submodo atual é mantido)");
        yield return new(M + "SetStandardMode", 1, (c, _, _) => { c.SendCommand("media-mode", 0); return null; },
            "0x116420 -> cameraSetMediaModeU(0): XU 6 cmd 00");
        yield return new(M + "GetVirtualBackgroundInfo", 1, (c, _, _) => new OscMessage(M + "VirtualBackgroundInfo", StateOf(c).VirtualBackground),
            "0x1201F0 -> sendVirtualBackgroundInfo (0x1224C0): ,i; aqui: o último SetVirtualBackground");
        yield return new(M + "GetAutoFramingInfo", 1, (c, _, _) => new OscMessage(M + "AutoFramingInfo", StateOf(c).AutoFraming),
            "0x120490 -> sendAutoFramingInfo (0x122850): ,i; aqui: o último SetAutoFraming");
        yield return new(M + "GetPresetPositionInfo", 1, (_, _, _) => PresetInfo(M),
            "0x11FCB0 -> sendPresetPositionInfo (0x121BE0): ,isisis");

        // --- vídeo no PC -----------------------------------------------------------
        yield return new(G + "PCSnapshot", 1, (_, _, _) => Media(m => m.Snapshot(SelectedDevice)),
            "0x1349F0: foto feita pelo processo de mídia do OBSBOT Center; aqui: pelo vídeo do obsbot-studio");
        yield return new(G + "SetPCRecording", 1, (_, _, v) => Media(m => m.SetRecording(SelectedDevice, v[0] == 1, highFps: false)),
            "0x1353D0: gravação no PC pelo processo de mídia; aqui: AVI MJPEG pelo vídeo do obsbot-studio");
        yield return new(G + "SetHighfpsRecording", 1, (_, _, v) => Media(m => m.SetRecording(SelectedDevice, v[0] == 1, highFps: true)),
            "0x135B80: gravação em alta taxa de quadros; aqui: igual a SetPCRecording, na taxa do vídeo aberto");

        // --- conhecidos, sem equivalente no protocolo V1 ------------------------------
        const string V3 = "o comando só existe no protocolo 0xAA V3 (a tabela V1 da libdev não tem esse id, então a Tiny não o recebe); o serializador V3 (RVA 0x1001B0) não foi implementado";
        foreach (var (addr, values, why) in new (string, int, string)[]
        {
            (G + "SetGimWhiteBalanceShift", 2, "0x11CF00 -> cameraSetWhiteBalanceSetting, set 1 id 0x0B: " + V3),
            (G + "SetRBGain", 2, "0x11D3E0 -> cameraSetWhiteBalanceSetting, set 1 id 0x0B: " + V3),
            (G + "SetZoneTrack", 1, "0x119620 -> aiSetZoneTrackStateR, set 3 id 0x5D: " + V3),
            (G + "TriggerZonePreset", 1, "0x119970 -> aiTrgZonePresetR, set 3 id 0x1D: " + V3),
            (T + "SetTrackingSpeed", 1, "0x117550 -> aiSetControlParaR(0, 5, 0/2/3), set 3 id 0x84: " + V3),
        })
            yield return new(addr, values, null, why);
    }

    /// <summary>What the bridge remembers per device for the Get*Info replies OBSBOT Center builds from its UI.</summary>
    private sealed class DeviceState
    {
        public bool Awake = true;
        public int AiMode;
        public int VirtualBackground;
        public int AutoFraming;
    }

    // Callers hold _cameraLock, so the dictionary needs no lock of its own.
    private DeviceState StateOf(ICamera c)
    {
        if (!_state.TryGetValue(c, out var s))
            _state[c] = s = new DeviceState();
        return s;
    }

    private OscMessage? WhenAutoExposure(ICamera c, OscMessage m, Action action)
    {
        if (c.IsAuto(UvcControl.Exposure) == false)
            _log($"{m.Address}: ignorado com exposição manual (o OBSBOT Center faz o mesmo)");
        else
            action();
        return null;
    }

    /// <summary>OSC value = EV x 10; libdev's table is 19 steps of 1/3 EV from -3 to +3, index 9 = 0 EV.</summary>
    internal static int EvIndex(int evTimesTen) => Math.Clamp((int)Math.Round(evTimesTen * 3 / 10.0) + 9, 0, 18);

    private static void RequireTypes(ICamera c, string route, params int[] types)
    {
        if (!types.Contains(c.ProductCode))
            throw new NotSupportedException($"{route}: o OBSBOT Center não envia isso ao tipo de produto {c.ProductCode}.");
    }

    private static void RequireNotTypes(ICamera c, string route, params int[] types)
    {
        if (types.Contains(c.ProductCode))
            throw new NotSupportedException($"{route}: o tipo de produto {c.ProductCode} usa a mensagem do protocolo V3, não implementada.");
    }

    /// <summary>gimbalSetSpeedPositionR as SetGimMotorDegree calls it: roll 0, every axis at the same speed.</summary>
    private static OscMessage? GimbalTo(ICamera c, double speed, int pan, int pitch)
    {
        RequireTypes(c, "gimbalSetSpeedPositionR", 0, 1);
        c.SendMessage(RemoV1.GimbalSpeedPosition(0, Math.Clamp(pitch, -59, 59), Math.Clamp(pan, -129, 129), speed, speed, speed));
        return null;
    }

    private static OscMessage? PtzValue(ICamera c, int pan, int pitch, int zoom)
    {
        RequireTypes(c, "SetPTZValue", 0, 1);
        pan = Math.Clamp(pan, -130, 130);
        pitch = Math.Clamp(pitch, -60, 60);
        c.SendMessage(RemoV1.TargetSelect(false));
        // OBSBOT reads the attitude with gimbalGetAttitudeInfoR. The UVC position is the same angle,
        // but gimbal pitch is the opposite of UVC tilt (tested on the Tiny: pitch +5 -> tilt -4).
        var (curPan, tilt) = c.GetPanTilt() ?? (0, 0);
        int curPitch = -tilt;
        double dPan = Math.Abs(pan - curPan), dPitch = Math.Abs(pitch - curPitch);
        double seconds = Math.Min(Math.Max(dPan, dPitch) / 20.0, 3.0);
        if (seconds > 0)
            c.SendMessage(RemoV1.GimbalSpeedPosition(0, pitch, pan, 0, dPitch / seconds, dPan / seconds));
        c.SetZoomPercent(Math.Clamp(zoom, 0, 100));
        return null;
    }

    /// <summary>OSC SetAiMode value -> (AiWorkModeType, sub mode) for types 0..3 and 12 (jump table at RVA 0x117164).</summary>
    internal static (int Mode, int Sub) AiModeFor(int value) => value switch
    {
        1 => (2, 0),
        2 => (2, 1),
        3 => (2, 2),
        4 => (2, 3),
        5 => (2, 4),
        6 => (5, 0),
        7 => (4, 0),
        8 => (3, 0),
        9 => (1, 0),
        _ => (0, 0),
    };

    private OscMessage? AiMode(ICamera c, int value)
    {
        int type = c.ProductCode;
        if (type is 18 or 19)
        {
            // Own sequence for these models (RVA 0x116D9C).
            (int, int)[] steps = value switch
            {
                0 => [(2, 0), (14, 0)],
                1 => [(2, 0), (14, 1)],
                2 => [(7, 0)],
                3 => [(5, 0)],
                4 => [(3, 0)],
                5 => [(4, 0)],
                _ => [],
            };
            foreach (var (mode, sub) in steps)
                c.SendCommand("ai-mode", mode, sub);
            StateOf(c).AiMode = steps.Length > 0 ? 1 : StateOf(c).AiMode;
            return null;
        }
        RequireTypes(c, "SetAiMode", 0, 1, 2, 3, 12);
        if (type is 3 or 12 && value is 6 or 7)
            return null;
        var (m, s) = AiModeFor(value);
        c.SendCommand("ai-mode", m, s);
        StateOf(c).AiMode = m;
        return null;
    }

    private static OscMessage? TrackingMode(ICamera c, int value)
    {
        int type = c.ProductCode;
        RequireTypes(c, "SetTrackingMode", 0, 1, 2, 3, 12);
        byte? mode = value switch
        {
            1 => 0,
            2 => 2,
            0 when type is 0 or 1 => 1,
            _ => null,
        };
        if (mode is { } m)
            c.SendMessage(RemoV1.TrackingMode(m));
        return null;
    }

    private OscMessage? AiLock(ICamera c, bool on)
    {
        switch (c.ProductCode)
        {
            case 0 or 1:
                c.SendMessage(RemoV1.TargetSelect(on));
                break;
            case 2 or 3 or 12 or 18 or 19:
                c.SendCommand("ai-mode", on ? 2 : 0, 0);
                break;
            default:
                throw new NotSupportedException($"ToggleAILock: o OBSBOT Center não envia isso ao tipo de produto {c.ProductCode}.");
        }
        StateOf(c).AiMode = on ? 2 : 0;
        return null;
    }

    private OscMessage? VirtualBackground(ICamera c, int value)
    {
        if (value == 0)
        {
            c.SendCommand("media-mode", 0);
        }
        else
        {
            int? background = value switch { 1 => 0x12, 2 => 1, 3 => 0x11, _ => null };
            c.SendCommand("media-mode", 1);
            if (background is { } b)
                c.SendCommand("bg-mode", b);
        }
        StateOf(c).VirtualBackground = value;
        return null;
    }

    private OscMessage? AutoFraming(ICamera c, int value)
    {
        c.SendCommand("media-mode", value == 0 ? 0 : 2);
        StateOf(c).AutoFraming = value == 0 ? 0 : 1;
        return null;
    }

    private static OscMessage PresetInfo(string page) =>
        new(page + "PresetPositionInfo", 0, "Preset 1", 1, "Preset 2", 2, "Preset 3");

    private OscMessage? Media(Action<IPcMedia> action)
    {
        if (_media is null)
            _log("sem vídeo neste processo: foto e gravação no PC só funcionam no obsbot-studio");
        else
            action(_media);
        return null;
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
        // Run status: the last WakeSleep sent (reading it needs a 0xAA reply, not decoded yet).
        args.Add(SelectedDevice < cams.Count && !StateOf(cams[SelectedDevice]).Awake ? 0 : 1);
        args.Add(SelectedDevice < cams.Count ? cams[SelectedDevice].ProductCode : 0);
        return new OscMessage(Prefix + "General/DeviceInfo", args);
    }
}
