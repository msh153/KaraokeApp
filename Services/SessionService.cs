using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using KaraokeApp.Models;

namespace KaraokeApp.Services;

/// <summary>
/// Manages the Wi-Fi peer-to-peer session between Host and Client phones.
/// Uses mDNS-style UDP broadcast for discovery + TCP for control.
/// </summary>
public class SessionService
{
    private const int DiscoveryPort = 5557;
    private const string BroadcastMagic = "KARAOKE_SESSION_V1";

    private UdpClient? _discoveryClient;
    private TcpListener? _controlListener;
    private TcpClient? _controlClient;
    private CancellationTokenSource? _cts;

    public SessionInfo? CurrentSession { get; private set; }
    public bool IsHost { get; private set; }

    public event EventHandler<SessionInfo>? SessionDiscovered;
    public event EventHandler<string>? ClientConnected;
    public event EventHandler? ClientDisconnected;
    public event EventHandler<string>? ControlMessageReceived;

    // ─── HOST ───────────────────────────────────────────────────────────────

    public async Task StartHostSessionAsync()
    {
        IsHost = true;
        _cts = new CancellationTokenSource();

        var localIp = GetLocalIpAddress();
        CurrentSession = new SessionInfo
        {
            HostIp = localIp,
            AudioPort = 5555,
            ControlPort = 5556,
            SessionId = Guid.NewGuid().ToString("N")[..8],
            IsActive = true
        };

        // Broadcast session info via UDP
        _ = Task.Run(() => BroadcastSessionAsync(_cts.Token), _cts.Token);

        // Listen for client control connection
        _ = Task.Run(() => ListenForClientAsync(_cts.Token), _cts.Token);
    }

    private async Task BroadcastSessionAsync(CancellationToken ct)
    {
        using var udp = new UdpClient();
        udp.EnableBroadcast = true;
        var endpoint = new IPEndPoint(IPAddress.Broadcast, DiscoveryPort);

        var payload = JsonSerializer.Serialize(CurrentSession);
        var data = Encoding.UTF8.GetBytes($"{BroadcastMagic}|{payload}");

        while (!ct.IsCancellationRequested)
        {
            udp.Send(data, data.Length, endpoint);
            await Task.Delay(1000, ct);
        }
    }

    private async Task ListenForClientAsync(CancellationToken ct)
    {
        _controlListener = new TcpListener(IPAddress.Any, CurrentSession!.ControlPort);
        _controlListener.Start();

        try
        {
            _controlClient = await _controlListener.AcceptTcpClientAsync(ct);
            var remote = ((IPEndPoint)_controlClient.Client.RemoteEndPoint!).Address.ToString();
            ClientConnected?.Invoke(this, remote);

            await HandleControlConnectionAsync(_controlClient, ct);
        }
        catch (OperationCanceledException) { }
        finally
        {
            _controlListener.Stop();
        }
    }

    // ─── CLIENT ─────────────────────────────────────────────────────────────

    public async Task<SessionInfo?> DiscoverHostAsync(TimeSpan timeout, CancellationToken ct = default)
    {
        using var udp = new UdpClient(DiscoveryPort);
        udp.Client.ReceiveTimeout = (int)timeout.TotalMilliseconds;

        using var combined = CancellationTokenSource.CreateLinkedTokenSource(ct);
        combined.CancelAfter(timeout);

        try
        {
            while (!combined.Token.IsCancellationRequested)
            {
                var result = await udp.ReceiveAsync(combined.Token);
                var text = Encoding.UTF8.GetString(result.Buffer);

                if (text.StartsWith(BroadcastMagic))
                {
                    var json = text[(BroadcastMagic.Length + 1)..];
                    var session = JsonSerializer.Deserialize<SessionInfo>(json);
                    return session;
                }
            }
        }
        catch (OperationCanceledException) { }

        return null;
    }

    public async Task<bool> JoinSessionAsync(SessionInfo session, CancellationToken ct = default)
    {
        IsHost = false;
        CurrentSession = session;
        _cts = new CancellationTokenSource();

        try
        {
            _controlClient = new TcpClient();
            await _controlClient.ConnectAsync(session.HostIp, session.ControlPort, ct);
            _ = Task.Run(() => HandleControlConnectionAsync(_controlClient, _cts.Token), _cts.Token);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Session] Join failed: {ex.Message}");
            return false;
        }
    }

    // ─── SHARED ─────────────────────────────────────────────────────────────

    private async Task HandleControlConnectionAsync(TcpClient client, CancellationToken ct)
    {
        var stream = client.GetStream();
        var buffer = new byte[1024];

        try
        {
            while (!ct.IsCancellationRequested && client.Connected)
            {
                var read = await stream.ReadAsync(buffer, ct);
                if (read == 0) break;

                var message = Encoding.UTF8.GetString(buffer, 0, read);
                ControlMessageReceived?.Invoke(this, message);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"[Session] Control error: {ex.Message}");
        }
        finally
        {
            ClientDisconnected?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task SendControlMessageAsync(string message)
    {
        if (_controlClient?.Connected != true) return;
        var data = Encoding.UTF8.GetBytes(message);
        await _controlClient.GetStream().WriteAsync(data);
    }

    public void StopSession()
    {
        _cts?.Cancel();
        _controlClient?.Close();
        _controlListener?.Stop();
        CurrentSession = null;
    }

    private static string GetLocalIpAddress()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
        socket.Connect("8.8.8.8", 65530);
        return (socket.LocalEndPoint as IPEndPoint)?.Address.ToString() ?? "127.0.0.1";
    }
}
