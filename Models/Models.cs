namespace KaraokeApp.Models;

public enum DeviceRole
{
    Unset,
    Host,
    Client
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

// AudioSettings lives in KaraokeApp.Services (IAudioPlatform.cs) — not duplicated here

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