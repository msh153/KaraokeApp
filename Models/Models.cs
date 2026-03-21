namespace KaraokeApp.Models;

public enum DeviceRole
{
    Unset,
    Host,   // Plays music + mic + receives mic from Client
    Client  // Only mic, streams to Host
}

public enum ConnectionState
{
    Disconnected,
    Scanning,
    Connecting,
    Connected,
    Error
}

public class BluetoothDevice
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = "Unknown Device";
    public bool IsConnected { get; set; }
    public int SignalStrength { get; set; }
    public bool IsJBL => Name.Contains("JBL", StringComparison.OrdinalIgnoreCase);
}

public class AudioSettings
{
    public float MusicVolume { get; set; } = 0.8f;
    public float Mic1Volume { get; set; } = 1.0f;
    public float Mic2Volume { get; set; } = 1.0f;
    public bool Mic1Muted { get; set; }
    public bool Mic2Muted { get; set; }
    public bool MusicMuted { get; set; }
    public int SampleRate { get; set; } = 44100;
    public int BufferSize { get; set; } = 4096;
}

public class StreamPacket
{
    public PacketType Type { get; set; }
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public long Timestamp { get; set; }
    public int SequenceNumber { get; set; }
}

public enum PacketType
{
    AudioMic,
    Control,
    Heartbeat,
    Disconnect
}

public class SessionInfo
{
    public string HostIp { get; set; } = string.Empty;
    public int AudioPort { get; set; } = 5555;
    public int ControlPort { get; set; } = 5556;
    public string SessionId { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public bool IsActive { get; set; }
}
