using System.Net;
using System.Net.Sockets;

namespace Obsbot.Osc;

/// <summary>Where replies to an incoming message go.</summary>
public interface IOscPeer
{
    string Description { get; }

    Task SendAsync(OscMessage message, CancellationToken ct);
}

public enum OscTransport
{
    Udp,
    Tcp,
}

/// <summary>
/// OSC server over UDP or TCP, the two "Connection Type" choices of OBSBOT Center
/// (setting OSCConnectMethod: 0 = UDP, 1 = TCP; liblo protocol LO_UDP = 1 / LO_TCP = 4).
/// As in OBSBOT Center, replies are sent back to the peer that sent the request:
/// the source endpoint for UDP, the same connection for TCP.
/// </summary>
public sealed class OscServer : IAsyncDisposable
{
    /// <summary>Default of the OSCReceivePort setting in OBSBOT Center's global.ini.</summary>
    public const int DefaultPort = 16284;

    private readonly Func<OscMessage, IOscPeer, CancellationToken, Task> _handler;
    private readonly Action<string> _log;
    private readonly CancellationTokenSource _cts = new();
    private readonly List<Task> _tasks = [];
    private UdpClient? _udp;
    private TcpListener? _tcp;

    public OscServer(OscTransport transport, IPEndPoint endpoint,
        Func<OscMessage, IOscPeer, CancellationToken, Task> handler, Action<string> log, int? udpReplyPort = null)
    {
        Transport = transport;
        _handler = handler;
        _log = log;
        UdpReplyPort = udpReplyPort;
        if (transport == OscTransport.Udp)
        {
            _udp = new UdpClient(endpoint);
            LocalEndPoint = (IPEndPoint)_udp.Client.LocalEndPoint!;
        }
        else
        {
            _tcp = new TcpListener(endpoint);
            _tcp.Start();
            LocalEndPoint = (IPEndPoint)_tcp.LocalEndpoint;
        }
    }

    public OscTransport Transport { get; }
    public IPEndPoint LocalEndPoint { get; }

    /// <summary>When set, UDP replies go to the sender's address but to this port.</summary>
    public int? UdpReplyPort { get; }

    public void Start() =>
        _tasks.Add(Transport == OscTransport.Udp ? Task.Run(UdpLoopAsync) : Task.Run(TcpAcceptLoopAsync));

    private async Task UdpLoopAsync()
    {
        var ct = _cts.Token;
        while (!ct.IsCancellationRequested)
        {
            UdpReceiveResult r;
            try
            {
                r = await _udp!.ReceiveAsync(ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionReset)
            {
                continue; // ICMP port unreachable from a previous reply
            }

            var target = UdpReplyPort is { } port ? new IPEndPoint(r.RemoteEndPoint.Address, port) : r.RemoteEndPoint;
            await DispatchAsync(r.Buffer, new UdpPeer(_udp!, target), ct);
        }
    }

    private async Task TcpAcceptLoopAsync()
    {
        var ct = _cts.Token;
        while (!ct.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _tcp!.AcceptTcpClientAsync(ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            lock (_tasks)
                _tasks.Add(Task.Run(() => TcpClientLoopAsync(client, ct)));
        }
    }

    private async Task TcpClientLoopAsync(TcpClient client, CancellationToken ct)
    {
        var remote = client.Client.RemoteEndPoint?.ToString() ?? "?";
        _log($"TCP: conectado {remote}");
        using (client)
        {
            var framing = new OscStreamFraming(client.GetStream());
            var peer = new TcpPeer(framing, remote);
            try
            {
                while (await framing.ReadPacketAsync(ct) is { } packet)
                    await DispatchAsync(packet, peer, ct);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or OperationCanceledException or SocketException)
            {
                if (!ct.IsCancellationRequested)
                    _log($"TCP: {remote}: {ex.Message}");
            }
        }
        _log($"TCP: desconectado {remote}");
    }

    private async Task DispatchAsync(byte[] packet, IOscPeer peer, CancellationToken ct)
    {
        IReadOnlyList<OscMessage> messages;
        try
        {
            messages = OscMessage.DecodePacket(packet);
        }
        catch (FormatException ex)
        {
            _log($"{peer.Description}: pacote OSC inválido ({ex.Message})");
            return;
        }
        foreach (var m in messages)
        {
            try
            {
                await _handler(m, peer, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log($"{peer.Description}: erro em {m.Address}: {ex.Message}");
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _udp?.Dispose();
        _tcp?.Stop();
        Task[] pending;
        lock (_tasks)
            pending = [.. _tasks];
        try
        {
            await Task.WhenAll(pending).WaitAsync(TimeSpan.FromSeconds(2));
        }
        catch (Exception)
        {
            // shutting down: socket errors and timeouts are expected here
        }
        _cts.Dispose();
        _udp = null;
        _tcp = null;
    }

    private sealed class UdpPeer(UdpClient socket, IPEndPoint target) : IOscPeer
    {
        public string Description => $"udp://{target}";

        public async Task SendAsync(OscMessage message, CancellationToken ct) =>
            await socket.SendAsync(message.Encode(), target, ct);
    }

    private sealed class TcpPeer(OscStreamFraming framing, string remote) : IOscPeer
    {
        private readonly SemaphoreSlim _writeLock = new(1, 1);

        public string Description => $"tcp://{remote}";

        public async Task SendAsync(OscMessage message, CancellationToken ct)
        {
            await _writeLock.WaitAsync(ct);
            try
            {
                await framing.WritePacketAsync(message.Encode(), ct);
            }
            finally
            {
                _writeLock.Release();
            }
        }
    }
}
