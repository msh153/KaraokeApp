using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using KaraokeApp.Models;

namespace KaraokeApp.Services;

public class SessionService
{
    private const int DiscoveryPort = 5557;
    private const string BroadcastMagic = "KARAOKE_SESSION_V1";

    private TcpListener? _controlListener;
    private TcpClient? _controlClient;
    private CancellationTokenSource? _cts;

    public SessionInfo? CurrentSession { get; private set; }
    public bool IsHost { get; private set; }

    public event EventHandler<string>? ClientConnected;
    public event EventHandler? ClientDisconnected;
    public event EventHandler<string>? ControlMessageReceived;

    // ─── HOST ────────────────────────────────────────────────────────────

    public async Task StartHostSessionAsync()
    {
        try
        {
            IsHost = true;
            _cts = new CancellationTokenSource();

            CurrentSession = new SessionInfo
            {
                HostIp = GetLocalIpAddress(),
                AudioPort = 5555,
                ControlPort = 5556,
                SessionId = Guid.NewGuid().ToString("N")[..8],
                IsActive = true
            };

            System.Diagnostics.Debug.WriteLine($"[SessionService] Стартуем Host сесію: {CurrentSession.SessionId} на IP {CurrentSession.HostIp}");

            _ = Task.Run(() => BroadcastSessionAsync(_cts.Token), _cts.Token);
            _ = Task.Run(() => ListenForClientAsync(_cts.Token), _cts.Token);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SessionService] Ошибка StartHostSessionAsync: {ex}");
            throw;
        }
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
            try { udp.Send(data, data.Length, endpoint); }
            catch { /* ignore send errors */ }
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
        finally { _controlListener.Stop(); }
    }

    // ─── CLIENT ──────────────────────────────────────────────────────────

    public async Task<SessionInfo?> DiscoverHostAsync(
        TimeSpan timeout, CancellationToken ct = default)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[SessionService] Поиск Host сесії с таймаутом {timeout.TotalSeconds}с...");

            using var udp = new UdpClient(DiscoveryPort);
            udp.Client.ReceiveTimeout = (int)timeout.TotalMilliseconds;
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linked.CancelAfter(timeout);

            while (!linked.Token.IsCancellationRequested)
            {
                var result = await udp.ReceiveAsync(linked.Token);
                var text = Encoding.UTF8.GetString(result.Buffer);
                if (text.StartsWith(BroadcastMagic))
                {
                    var json = text[(BroadcastMagic.Length + 1)..];
                    var session = JsonSerializer.Deserialize<SessionInfo>(json);
                    System.Diagnostics.Debug.WriteLine($"[SessionService] Найдена Host сесія: {session?.SessionId} на {session?.HostIp}");
                    return session;
                }
            }

            System.Diagnostics.Debug.WriteLine("[SessionService] Host не найден");
            return null;
        }
        catch (OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine("[SessionService] Поиск Host отменен (таймаут)");
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SessionService] Ошибка DiscoverHostAsync: {ex}");
            throw;
        }
    }

    public async Task<bool> JoinSessionAsync(
        SessionInfo session, CancellationToken ct = default)
    {
        try
        {
            IsHost = false;
            CurrentSession = session;
            _cts = new CancellationTokenSource();

            System.Diagnostics.Debug.WriteLine($"[SessionService] Присоединяемся к Host сесії {session.SessionId} на {session.HostIp}:{session.ControlPort}...");

            _controlClient = new TcpClient();
            await _controlClient.ConnectAsync(session.HostIp, session.ControlPort, ct);
            
            System.Diagnostics.Debug.WriteLine("[SessionService] Успешно присоединены к Host сесії");
            
            _ = Task.Run(
                () => HandleControlConnectionAsync(_controlClient, _cts.Token),
                _cts.Token);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SessionService] Ошибка JoinSessionAsync: {ex.Message}");
            return false;
        }
    }

    // ─── SHARED ──────────────────────────────────────────────────────────

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
                var msg = Encoding.UTF8.GetString(buffer, 0, read);
                ControlMessageReceived?.Invoke(this, msg);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"[Session] Control error: {ex.Message}");
        }
        finally { ClientDisconnected?.Invoke(this, EventArgs.Empty); }
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
        using var socket = new Socket(
            AddressFamily.InterNetwork, SocketType.Dgram, 0);
        socket.Connect("8.8.8.8", 65530);
        return (socket.LocalEndPoint as IPEndPoint)?.Address.ToString()
               ?? "127.0.0.1";
    }
}