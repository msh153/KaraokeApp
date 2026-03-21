using System.Net;
using System.Net.Sockets;
using KaraokeApp.Models;

namespace KaraokeApp.Services;

/// <summary>
/// Manages audio capture, mixing and playback.
/// HOST: captures mic1, receives mic2 from network, plays music, mixes all → speaker
/// CLIENT: captures mic2, streams to host via UDP
/// </summary>
public interface IAudioService
{
    AudioSettings Settings { get; }
    bool IsCapturing { get; }
    bool IsStreaming { get; }

    event EventHandler<float> Mic1LevelChanged;
    event EventHandler<float> Mic2LevelChanged;

    Task StartHostModeAsync(SessionInfo session);
    Task StartClientModeAsync(SessionInfo session);
    Task StopAsync();

    void UpdateSettings(AudioSettings settings);
    Task<string?> PickMusicFileAsync();
    void PlayMusic(string filePath);
    void PauseMusic();
    void StopMusic();
}

public class AudioService : IAudioService
{
    private readonly IAudioCapture _audioCapture;
    private readonly IAudioPlayer _audioPlayer;
    private UdpClient? _udpSend;
    private UdpClient? _udpReceive;
    private CancellationTokenSource? _cts;
    private bool _isHost;
    private SessionInfo? _session;

    public AudioSettings Settings { get; private set; } = new();
    public bool IsCapturing { get; private set; }
    public bool IsStreaming { get; private set; }

    public event EventHandler<float>? Mic1LevelChanged;
    public event EventHandler<float>? Mic2LevelChanged;

    public AudioService(IAudioCapture audioCapture, IAudioPlayer audioPlayer)
    {
        _audioCapture = audioCapture;
        _audioPlayer = audioPlayer;
    }

    public async Task StartHostModeAsync(SessionInfo session)
    {
        _isHost = true;
        _session = session;
        _cts = new CancellationTokenSource();

        // Start local mic capture
        await _audioCapture.StartCaptureAsync(Settings, OnMic1DataCaptured, _cts.Token);

        // Listen for client mic2 data
        _ = Task.Run(() => ListenForClientAudioAsync(_cts.Token), _cts.Token);

        IsCapturing = true;
        IsStreaming = false;
    }

    public async Task StartClientModeAsync(SessionInfo session)
    {
        _isHost = false;
        _session = session;
        _cts = new CancellationTokenSource();

        _udpSend = new UdpClient();
        _udpSend.Connect(session.HostIp, session.AudioPort);

        // Start mic capture and stream to host
        await _audioCapture.StartCaptureAsync(Settings, OnMic2DataCaptured, _cts.Token);

        IsCapturing = true;
        IsStreaming = true;
    }

    private void OnMic1DataCaptured(byte[] pcmData)
    {
        var level = CalculateLevel(pcmData);
        Mic1LevelChanged?.Invoke(this, level);

        // Host plays directly to output (mixed)
        if (_isHost)
            _audioPlayer.WriteMixBuffer(pcmData, AudioChannel.Mic1, Settings.Mic1Volume);
    }

    private void OnMic2DataCaptured(byte[] pcmData)
    {
        var level = CalculateLevel(pcmData);
        Mic2LevelChanged?.Invoke(this, level);

        if (!_isHost)
        {
            // Client: stream to host
            SendAudioToHost(pcmData);
        }
        else
        {
            // Host's own mic2 (if device acts as both)
            _audioPlayer.WriteMixBuffer(pcmData, AudioChannel.Mic2, Settings.Mic2Volume);
        }
    }

    private void SendAudioToHost(byte[] pcmData)
    {
        if (_udpSend == null || _session == null) return;
        try
        {
            var packet = BuildPacket(PacketType.AudioMic, pcmData);
            _udpSend.Send(packet, packet.Length);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AudioService] Send error: {ex.Message}");
        }
    }

    private async Task ListenForClientAudioAsync(CancellationToken ct)
    {
        if (_session == null) return;

        _udpReceive = new UdpClient(_session.AudioPort);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var result = await _udpReceive.ReceiveAsync(ct);
                var (type, data) = ParsePacket(result.Buffer);

                if (type == PacketType.AudioMic)
                {
                    var level = CalculateLevel(data);
                    Mic2LevelChanged?.Invoke(this, level);
                    _audioPlayer.WriteMixBuffer(data, AudioChannel.Mic2, Settings.Mic2Volume);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AudioService] Receive error: {ex.Message}");
        }
        finally
        {
            _udpReceive?.Close();
        }
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();
        await _audioCapture.StopCaptureAsync();
        _audioPlayer.StopAll();
        _udpSend?.Close();
        _udpReceive?.Close();
        IsCapturing = false;
        IsStreaming = false;
    }

    public void UpdateSettings(AudioSettings settings)
    {
        Settings = settings;
        _audioPlayer.UpdateVolumes(settings);
    }

    public async Task<string?> PickMusicFileAsync()
    {
        var options = new PickOptions
        {
            PickerTitle = "Оберіть музичний файл",
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.Android, new[] { "audio/*" } },
                { DevicePlatform.iOS, new[] { "public.audio" } }
            })
        };

        var result = await FilePicker.Default.PickAsync(options);
        return result?.FullPath;
    }

    public void PlayMusic(string filePath)
    {
        _audioPlayer.PlayMusic(filePath, Settings.MusicVolume);
    }

    public void PauseMusic() => _audioPlayer.PauseMusic();
    public void StopMusic() => _audioPlayer.StopMusic();

    private static float CalculateLevel(byte[] pcmData)
    {
        if (pcmData.Length < 2) return 0f;
        long sum = 0;
        for (int i = 0; i < pcmData.Length - 1; i += 2)
        {
            short sample = BitConverter.ToInt16(pcmData, i);
            sum += sample * sample;
        }
        var rms = Math.Sqrt((double)sum / (pcmData.Length / 2));
        return (float)Math.Min(rms / 32768.0, 1.0);
    }

    private static byte[] BuildPacket(PacketType type, byte[] data)
    {
        // Header: 1 byte type + 4 bytes length + data
        var packet = new byte[5 + data.Length];
        packet[0] = (byte)type;
        var lenBytes = BitConverter.GetBytes(data.Length);
        Buffer.BlockCopy(lenBytes, 0, packet, 1, 4);
        Buffer.BlockCopy(data, 0, packet, 5, data.Length);
        return packet;
    }

    private static (PacketType type, byte[] data) ParsePacket(byte[] raw)
    {
        if (raw.Length < 5) return (PacketType.Control, Array.Empty<byte>());
        var type = (PacketType)raw[0];
        var len = BitConverter.ToInt32(raw, 1);
        var data = new byte[Math.Min(len, raw.Length - 5)];
        Buffer.BlockCopy(raw, 5, data, 0, data.Length);
        return (type, data);
    }
}

public enum AudioChannel { Mic1, Mic2, Music }
