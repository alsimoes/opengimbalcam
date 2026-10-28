using System.Globalization;
using Obsbot.Core.Camera;
using Obsbot.Core.Firmware;
using Obsbot.Core.Protocol;
using Obsbot.Devices;

namespace ObsbotCtl;

internal static class Program
{
    private const string Usage = """
        obsbotctl - controle aberto de câmeras OBSBOT no Windows e no Linux (UVC + Extension Unit)

        Dispositivo:
          list                                  Lista câmeras de vídeo (marca as OBSBOT, VID 3564)
          info                                  Topologia UVC, onde está a XU OBSBOT e controles UVC padrão
          get <controle>                        Lê um controle UVC padrão (pan, tilt, zoom, focus, brightness...)
          set <controle> <valor|auto>           Escreve um controle UVC padrão
          move <pan> <tilt> [--ms 500]          Pan/tilt relativo via XU (velocidades -255..255), depois para
          cmds                                  Lista os comandos diretos conhecidos (seletor 6)
          cmd <nome> <args...>                  Envia um comando direto (seletor 6)
          preset <trigger|delete> <id>          Presets do gimbal em modelos antigos (seletor 7)
          xu-get <seletor> [--len 60]           Leitura bruta da Extension Unit
          xu-set <seletor> <hex> [--force]      Escrita bruta (seletores 1,3,4,5 exigem --force)
          msg-read                              Lê a caixa de mensagens (seletor 2) e decodifica
          msg-send --sender N --receiver N --set N --id N [--type 0|1|2] [--seq N] [--payload hex]
                                                Envia uma mensagem 0xAA (campos de fio) e lê a resposta

        OSC (protocolo do OBSBOT Center, para TouchOSC, Companion, Stream Deck...):
          osc serve|send|routes                 Veja 'obsbotctl osc help'

        Firmware (somente leitura, nunca grava na câmera):
          fw info <arquivo.bin>                 Cabeçalho, componentes e partições AMAC
          fw verify <arquivo.bin>               Confere MD5/CRC-32 de tudo
          fw extract <arquivo.bin> <pasta>      Extrai componentes e partições

        Utilitários:
          msg-decode <hex>                      Decodifica um quadro 0xAA

        Opções globais: -d <índice> escolhe o dispositivo (padrão: primeira OBSBOT).
        """;

    private static int Main(string[] argv)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        var args = new Args(argv);
        try
        {
            return args.Command switch
            {
                "list" => List(),
                "info" => WithCamera(args, Info),
                "get" => WithCamera(args, s => GetControl(s, args.Positional(0))),
                "set" => WithCamera(args, s => SetControl(s, args.Positional(0), args.Positional(1))),
                "move" => WithCamera(args, s => Move(s, args)),
                "cmds" => ListCommands(),
                "cmd" => WithCamera(args, s => SendCommand(s, args)),
                "preset" => WithCamera(args, s => Preset(s, args)),
                "xu-get" => WithCamera(args, s => XuGet(s, args)),
                "xu-set" => WithCamera(args, s => XuSet(s, args)),
                "msg-read" => WithCamera(args, MsgRead),
                "msg-send" => WithCamera(args, s => MsgSend(s, args)),
                "msg-decode" => MsgDecode(args.Positional(0)),
                "fw" => Firmware(args),
                "osc" => OscCommands.Run(argv.Skip(1)),
                null or "help" or "-h" or "--help" => Help(),
                _ => Fail($"comando desconhecido: {args.Command}"),
            };
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidDataException or XuException or NotSupportedException
                                       or FormatException or IOException or PlatformNotSupportedException
                                       or System.Runtime.InteropServices.COMException)
        {
            return Fail(ex.Message);
        }
    }

    private static int Help()
    {
        Console.WriteLine(Usage);
        return 0;
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine($"erro: {message}");
        return 1;
    }

    private static readonly Lazy<ICameraPlatform> Platform = new(CameraPlatform.Create);

    private static int List()
    {
        var devices = Platform.Value.Enumerate();
        if (devices.Count == 0)
        {
            Console.WriteLine($"Nenhuma câmera de vídeo encontrada ({Platform.Value.Name}).");
            return 0;
        }
        foreach (var d in devices)
        {
            Console.WriteLine($"[{d.Index}] {(d.IsObsbot ? "*" : " ")} {d.Name}  ({d.UsbId})");
            if (d.Path is not null)
                Console.WriteLine($"       {d.Path}");
        }
        return 0;
    }

    private static int WithCamera(Args args, Func<ICameraSession, int> action)
    {
        var devices = Platform.Value.Enumerate();
        CameraInfo? device = args.Option("-d") is { } idx
            ? devices.FirstOrDefault(d => d.Index == int.Parse(idx, CultureInfo.InvariantCulture))
            : devices.FirstOrDefault(d => d.IsObsbot);
        if (device is null)
            return Fail("nenhuma câmera OBSBOT encontrada (use 'list' e -d <índice>).");

        using var session = Platform.Value.Open(device);
        return action(session);
    }

    private static int Info(ICameraSession s)
    {
        Console.WriteLine($"Plataforma:  {Platform.Value.Name}");
        Console.WriteLine($"Dispositivo: {s.Info.Name}  ({s.Info.UsbId})");
        Console.WriteLine($"Caminho:     {s.Info.Path}");
        Console.WriteLine();
        foreach (var line in s.DescribeTopology())
            Console.WriteLine(line);

        var xu = s.ExtensionUnit;
        Console.WriteLine();
        Console.WriteLine(xu is null
            ? $"XU OBSBOT {ObsbotXu.Guid:B} não encontrada."
            : $"XU OBSBOT {xu.Guid:B} respondeu em {xu.Location}.");

        Console.WriteLine();
        Console.WriteLine("Controles UVC padrão:");
        foreach (var c in Enum.GetValues<CameraControl>())
            PrintControl(s, c);
        return 0;
    }

    private static void PrintControl(ICameraSession s, CameraControl control)
    {
        if (s.GetRange(control) is not { } r)
            return;
        var cur = s.Get(control) is { } v ? $"{v.Value} ({v.Flags})" : "?";
        Console.WriteLine($"  {control,-22} atual={cur,-14} faixa=[{r.Min}..{r.Max}] passo={r.Step} padrão={r.Default} caps={r.Caps}");
    }

    private static int GetControl(ICameraSession s, string name)
    {
        if (!TryParse<CameraControl>(name, out var c))
            return Fail($"controle desconhecido: {name}");
        PrintControl(s, c);
        return 0;
    }

    private static int SetControl(ICameraSession s, string name, string value)
    {
        if (!TryParse<CameraControl>(name, out var c))
            return Fail($"controle desconhecido: {name}");
        bool auto = value.Equals("auto", StringComparison.OrdinalIgnoreCase);
        int v = auto ? s.Get(c)?.Value ?? 0 : ParseInt(value);
        s.Set(c, v, auto ? ControlFlags.Auto : ControlFlags.Manual);
        PrintControl(s, c);
        return 0;
    }

    private static int Move(ICameraSession s, Args args)
    {
        var xu = s.RequireXu();
        int pan = ParseInt(args.Positional(0));
        int tilt = ParseInt(args.Positional(1));
        int ms = args.Option("--ms") is { } m ? ParseInt(m) : 500;

        xu.Set((uint)ObsbotXu.Selector.Command, DirectCommands.PanTiltRelative(pan, tilt));
        Thread.Sleep(ms);
        xu.Set((uint)ObsbotXu.Selector.Command, DirectCommands.PanTiltRelative(0, 0));
        Console.WriteLine($"pan={pan} tilt={tilt} por {ms} ms, parado.");
        return 0;
    }

    private static int ListCommands()
    {
        Console.WriteLine("Comandos diretos (XU seletor 6, quadro [id][len][payload] em 60 bytes):");
        foreach (var c in DirectCommands.All)
        {
            var args = c.Kind == ArgKind.UInt16 ? "<u16>" : string.Join(' ', Enumerable.Range(0, c.Length).Select(i => $"<b{i}>"));
            Console.WriteLine($"  {c.Name,-24} id=0x{c.Id:X2} len={c.Length} {args,-16} libdev: {c.LibdevFunction} @0x{c.CallSite:X}");
        }
        return 0;
    }

    private static int SendCommand(ICameraSession s, Args args)
    {
        var name = args.Positional(0);
        var cmd = DirectCommands.Find(name) ?? throw new ArgumentException($"comando desconhecido: {name} (veja 'cmds')");
        var values = args.PositionalFrom(1).Select(ParseInt).ToList();
        var payload = cmd.Build(values);
        s.RequireXu().Set((uint)ObsbotXu.Selector.Command, payload);
        Console.WriteLine($"enviado {cmd.Name}: {Convert.ToHexString(payload)}");
        return 0;
    }

    private static int Preset(ICameraSession s, Args args)
    {
        var action = args.Positional(0).ToLowerInvariant() switch
        {
            "trigger" => GimbalPresetAction.Trigger,
            "delete" => GimbalPresetAction.Delete,
            var a => throw new ArgumentException($"ação de preset desconhecida: {a}"),
        };
        var payload = DirectCommands.GimbalPreset(action, ParseInt(args.Positional(1)));
        s.RequireXu().Set((uint)ObsbotXu.Selector.GimbalPreset, payload);
        Console.WriteLine($"enviado preset {action}: {Convert.ToHexString(payload)}");
        return 0;
    }

    private static int XuGet(ICameraSession s, Args args)
    {
        uint selector = (uint)ParseInt(args.Positional(0));
        int len = args.Option("--len") is { } l ? ParseInt(l) : ObsbotXu.PayloadLength;
        var data = s.RequireXu().Get(selector, len);
        Console.WriteLine(Convert.ToHexString(data));
        return 0;
    }

    private static int XuSet(ICameraSession s, Args args)
    {
        uint selector = (uint)ParseInt(args.Positional(0));
        if (ObsbotXu.IsDangerous(selector) && !args.Flag("--force"))
            return Fail($"o seletor {selector} mexe em firmware/flash ou executa comandos no aparelho; use --force se souber o que está fazendo.");
        var payload = Convert.FromHexString(args.Positional(1).Replace(" ", ""));
        s.RequireXu().Set(selector, payload);
        Console.WriteLine($"SET seletor {selector}: {Convert.ToHexString(payload)}");
        return 0;
    }

    private static int MsgRead(ICameraSession s)
    {
        var data = s.RequireXu().Get((uint)ObsbotXu.Selector.Message);
        Console.WriteLine($"bruto: {Convert.ToHexString(data)}");
        var msg = RemoMessage.TryDecode(data, out var error);
        Console.WriteLine(msg is null ? $"não decodificado: {error}" : $"quadro: {msg}");
        return 0;
    }

    private static int MsgSend(ICameraSession s, Args args)
    {
        var msg = new RemoMessage(
            (RemoPacketType)(args.Option("--type") is { } t ? ParseInt(t) : (int)RemoPacketType.Type2),
            (ushort)(args.Option("--seq") is { } q ? ParseInt(q) : Random.Shared.Next(1, 0xFFFF)),
            (byte)ParseInt(args.Option("--sender") ?? throw new ArgumentException("--sender obrigatório")),
            (byte)ParseInt(args.Option("--receiver") ?? throw new ArgumentException("--receiver obrigatório")),
            (byte)ParseInt(args.Option("--set") ?? throw new ArgumentException("--set obrigatório")),
            (ushort)ParseInt(args.Option("--id") ?? throw new ArgumentException("--id obrigatório")),
            args.Option("--payload") is { } p ? Convert.FromHexString(p) : []);
        var frame = msg.Encode();
        var xu = s.RequireXu();
        xu.Set((uint)ObsbotXu.Selector.Message, frame);
        Console.WriteLine($"enviado: {Convert.ToHexString(frame)}");
        Thread.Sleep(50);
        return MsgRead(s);
    }

    private static int MsgDecode(string hex)
    {
        var msg = RemoMessage.TryDecode(Convert.FromHexString(hex.Replace(" ", "")), out var error);
        Console.WriteLine(msg?.ToString() ?? $"não decodificado: {error}");
        return msg is null ? 1 : 0;
    }

    private static int Firmware(Args args)
    {
        var sub = args.Positional(0);
        var pkg = FirmwarePackage.Open(args.Positional(1));
        switch (sub)
        {
            case "info":
                Console.WriteLine($"Produto: {pkg.Product}  Região: {pkg.Region}  Versão: {pkg.Version}  SoC: {pkg.Chip}  Build: {pkg.Build}");
                Console.WriteLine($"Cabeçalho: {pkg.HeaderLength} bytes, CRC-32 {(pkg.HeaderCrcOk ? "ok" : "INVÁLIDO")}; arquivo {pkg.TotalLength} bytes, MD5 do payload {Convert.ToHexString(pkg.PayloadMd5)}");
                Console.WriteLine("Componentes:");
                foreach (var c in pkg.Components)
                    Console.WriteLine($"  [{c.Index}] tipo={c.Type,-3} v{c.Version,-12} off=0x{c.AbsoluteOffset:X8} tam={c.Size,10}  {c.Name}");
                if (pkg.Partitions.Count > 0)
                {
                    Console.WriteLine("Partições do contêiner AMAC (imagem Linux SigmaStar):");
                    foreach (var p in pkg.Partitions)
                        Console.WriteLine($"  [{p.Index}] tipo={p.Type,-3} v{p.Version,-12} off=0x{p.AbsoluteOffset:X8} tam={p.Size,10}  {p.Name}");
                }
                return 0;
            case "verify":
                bool ok = pkg.HeaderCrcOk;
                bool payload = pkg.VerifyPayloadMd5();
                Console.WriteLine($"cabeçalho CRC-32: {(pkg.HeaderCrcOk ? "ok" : "FALHOU")}; MD5 do payload: {(payload ? "ok" : "FALHOU")}");
                ok &= payload;
                foreach (var r in pkg.Verify())
                {
                    var crc = r.Crc32Ok is { } c ? (c ? " CRC-32 ok" : " CRC-32 FALHOU") : "";
                    Console.WriteLine($"  {r.Entry.Name,-55} MD5 {(r.Md5Ok ? "ok" : "FALHOU")}{crc}; entrada {(r.Entry.EntryCrcOk ? "ok" : "FALHOU")}");
                    ok &= r.Md5Ok && r.Crc32Ok != false && r.Entry.EntryCrcOk;
                }
                Console.WriteLine(ok ? "Pacote íntegro." : "Pacote com falhas de integridade!");
                return ok ? 0 : 2;
            case "extract":
                foreach (var f in pkg.Extract(args.Positional(2)))
                    Console.WriteLine(f);
                return 0;
            default:
                return Fail($"subcomando de firmware desconhecido: {sub}");
        }
    }

    private static bool TryParse<T>(string name, out T value) where T : struct, Enum =>
        Enum.TryParse(name, ignoreCase: true, out value) && Enum.IsDefined(value);

    private static int ParseInt(string s) =>
        s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? int.Parse(s.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture)
            : int.Parse(s, NumberStyles.Integer, CultureInfo.InvariantCulture);

    /// <summary>Tiny argument parser: command, positionals, "--name value" options and bare flags.</summary>
    private sealed class Args
    {
        private static readonly HashSet<string> Flags = ["--force"];
        private readonly List<string> _positional = [];
        private readonly Dictionary<string, string> _options = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _flags = new(StringComparer.OrdinalIgnoreCase);

        public Args(string[] argv)
        {
            for (int i = 0; i < argv.Length; i++)
            {
                var a = argv[i];
                if (Flags.Contains(a))
                    _flags.Add(a);
                else if (a.StartsWith('-') && a.Length > 1 && !char.IsDigit(a[1]) && i + 1 < argv.Length)
                    _options[a] = argv[++i];
                else if (Command is null)
                    Command = a;
                else
                    _positional.Add(a);
            }
        }

        public string? Command { get; }

        public string Positional(int i) =>
            i < _positional.Count ? _positional[i] : throw new ArgumentException($"argumento {i + 1} ausente (veja 'help').");

        public IEnumerable<string> PositionalFrom(int i) => _positional.Skip(i);

        public string? Option(string name) => _options.GetValueOrDefault(name);

        public bool Flag(string name) => _flags.Contains(name);
    }
}
