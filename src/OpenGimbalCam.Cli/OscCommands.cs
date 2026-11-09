using System.Globalization;
using System.Net;
using System.Net.Sockets;
using OpenGimbalCam.Devices;
using OpenGimbalCam.Osc;
using OpenGimbalCam.Osc.Bridge;

namespace OpenGimbalCam.Cli;

/// <summary>
/// "ogcctl osc": OSC server compatible with OBSBOT Center's "OBSBOT OSC Protocol"
/// (TouchOSC, Bitfocus Companion, Stream Deck plugins...), without OBSBOT Center.
/// </summary>
internal static class OscCommands
{
    private static bool _quiet;

    public static int Run(IEnumerable<string> argv)
    {
        var args = argv.ToList();
        string? command = args.Count > 0 ? args[0] : null;
        try
        {
            return command switch
            {
                "serve" => Serve(args.Skip(1).ToList()),
                "send" => Send(args.Skip(1).ToList()),
                "routes" => Routes(),
                null or "help" or "-h" or "--help" => Help(),
                _ => Fail($"comando desconhecido: {command}"),
            };
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or SocketException or IOException or NotSupportedException)
        {
            return Fail(ex.Message);
        }
    }

    private static int Help()
    {
        Console.WriteLine("""
            ogcctl osc: servidor OSC compatível com o OBSBOT Center, sem o OBSBOT Center.

            uso:
              ogcctl osc serve [--udp] [--tcp] [--port 16284] [--bind 0.0.0.0] [--reply-port N]
                               [--obsbot-compat on|off] [-q]
                  Escuta mensagens /OBSBOT/WebCam/... e controla as câmeras OBSBOT (VID 3564).
                  Sem --udp/--tcp usa UDP, o padrão do OBSBOT Center. As respostas vão para
                  quem enviou (UDP: endereço de origem, ou --reply-port; TCP: a mesma conexão).
                  --obsbot-compat: liga (padrão) ou desliga a compatibilidade com o protocolo
                  OSC do OBSBOT Center. Desligada, as mensagens /OBSBOT/WebCam/... são ignoradas.
              ogcctl osc send <host>[:porta] <endereço> [args...] [--tcp] [--slip] [--wait ms]
                  Cliente de teste: envia uma mensagem e mostra as respostas.
                  Argumentos: 12 (int), 1.5 (float), qualquer outro texto (string).
              ogcctl osc routes
                  Lista os endereços do protocolo e o que o OBSBOT Center faz em cada um.

            Feche o OBSBOT Center (ou desligue o OSC nele) antes: os dois usariam a mesma porta.
            """);
        return 0;
    }

    private static int Routes()
    {
        var bridge = new OscBridge(new EmptyHub(), _ => { });
        foreach (var r in bridge.Routes.OrderBy(r => r.Handler is null).ThenBy(r => r.Address, StringComparer.Ordinal))
            Console.WriteLine($"{(r.Handler is null ? "  -" : "  +")} {r.Address,-48} {new string('i', r.Values),-4} {r.Evidence}");
        Console.WriteLine("\n+ implementado   - conhecido, não implementado (o motivo está ao lado)");
        return 0;
    }

    private static int Serve(List<string> args)
    {
        _quiet = Flag(args, "-q");
        bool udp = Flag(args, "--udp"), tcp = Flag(args, "--tcp");
        if (!udp && !tcp)
            udp = true;
        int port = Option(args, "--port") is { } p ? int.Parse(p, CultureInfo.InvariantCulture) : OscServer.DefaultPort;
        var bind = Option(args, "--bind") is { } b ? IPAddress.Parse(b) : IPAddress.Any;
        int? replyPort = Option(args, "--reply-port") is { } rp ? int.Parse(rp, CultureInfo.InvariantCulture) : null;
        bool compat = Option(args, "--obsbot-compat") switch
        {
            null or "on" => true,
            "off" => false,
            var v => throw new ArgumentException($"--obsbot-compat espera on ou off, recebeu {v}"),
        };
        RejectLeftovers(args);

        var platform = CameraPlatform.Create();
        using var hub = new ObsbotCameraHub(platform);
        Log($"plataforma: {platform.Name}");
        Log($"câmeras OBSBOT: {(hub.Cameras.Count == 0 ? "nenhuma" : string.Join(", ", hub.Cameras.Select((c, i) => $"[{i}] {c.Name}")))}");
        var bridge = new OscBridge(hub, Log) { ObsbotCompatibility = compat };
        Log($"compatibilidade com o protocolo OSC do OBSBOT Center: {(compat ? "ligada" : "desligada")}");

        var servers = new List<OscServer>();
        try
        {
            if (udp)
                servers.Add(new OscServer(OscTransport.Udp, new IPEndPoint(bind, port), bridge.HandleAsync, Log, replyPort));
            if (tcp)
                servers.Add(new OscServer(OscTransport.Tcp, new IPEndPoint(bind, port), bridge.HandleAsync, Log));
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
        {
            return Fail($"porta {port} em uso. O OBSBOT Center está aberto com OSC ligado?");
        }

        foreach (var s in servers)
        {
            s.Start();
            Log($"escutando OSC/{s.Transport.ToString().ToUpperInvariant()} em {s.LocalEndPoint}");
        }
        Log("Ctrl+C para sair.");

        using var stop = new ManualResetEventSlim();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Set(); };
        stop.Wait();
        foreach (var s in servers)
            s.DisposeAsync().AsTask().GetAwaiter().GetResult();
        return 0;
    }

    private static int Send(List<string> args)
    {
        bool tcp = Flag(args, "--tcp"), slip = Flag(args, "--slip");
        int wait = Option(args, "--wait") is { } w ? int.Parse(w, CultureInfo.InvariantCulture) : 500;
        if (args.Count < 2)
            throw new ArgumentException("uso: send <host>[:porta] <endereço> [args...]");
        var target = ParseEndPoint(args[0]);
        var message = new OscMessage(args[1], args.Skip(2).Select(ParseArg).ToArray());
        Console.WriteLine($"-> {message}");

        using var cts = new CancellationTokenSource(wait);
        var replies = new List<OscMessage>();
        if (tcp)
        {
            using var client = new TcpClient();
            client.Connect(target);
            var framing = new OscStreamFraming(client.GetStream(), slip ? OscFraming.Slip : OscFraming.LengthPrefixed);
            framing.WritePacketAsync(message.Encode(), CancellationToken.None).GetAwaiter().GetResult();
            try
            {
                while (framing.ReadPacketAsync(cts.Token).GetAwaiter().GetResult() is { } packet)
                    replies.AddRange(OscMessage.DecodePacket(packet));
            }
            catch (OperationCanceledException)
            {
            }
        }
        else
        {
            using var udp = new UdpClient(new IPEndPoint(IPAddress.Any, 0));
            udp.Send(message.Encode(), target);
            try
            {
                while (true)
                    replies.AddRange(OscMessage.DecodePacket(udp.ReceiveAsync(cts.Token).AsTask().GetAwaiter().GetResult().Buffer));
            }
            catch (OperationCanceledException)
            {
            }
        }

        foreach (var r in replies)
            Console.WriteLine($"<- {r}");
        if (replies.Count == 0)
            Console.WriteLine($"(sem resposta em {wait} ms)");
        return 0;
    }

    private static object ParseArg(string s)
    {
        if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
            return i;
        if (s.Contains('.') && float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
            return f;
        return s;
    }

    private static IPEndPoint ParseEndPoint(string s)
    {
        if (IPEndPoint.TryParse(s, out var ep))
            return ep.Port == 0 ? new IPEndPoint(ep.Address, OscServer.DefaultPort) : ep;
        var parts = s.Split(':', 2);
        var address = Dns.GetHostAddresses(parts[0]).First(a => a.AddressFamily == AddressFamily.InterNetwork);
        return new IPEndPoint(address, parts.Length == 2 ? int.Parse(parts[1], CultureInfo.InvariantCulture) : OscServer.DefaultPort);
    }

    private static bool Flag(List<string> args, string name) => args.Remove(name);

    private static string? Option(List<string> args, string name)
    {
        int i = args.IndexOf(name);
        if (i < 0)
            return null;
        if (i + 1 >= args.Count)
            throw new ArgumentException($"{name} precisa de um valor");
        var v = args[i + 1];
        args.RemoveRange(i, 2);
        return v;
    }

    private static void RejectLeftovers(List<string> args)
    {
        if (args.Count > 0)
            throw new ArgumentException($"argumento inesperado: {args[0]}");
    }

    private static void Log(string text)
    {
        if (!_quiet)
            Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} {text}");
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine($"erro: {message}");
        return 1;
    }

    private sealed class EmptyHub : ICameraHub
    {
        public IReadOnlyList<ICamera> Cameras => [];

        public void Refresh()
        {
        }
    }
}
