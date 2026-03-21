// Shared audio interfaces — implemented per platform in Platforms/Android/ and Platforms/iOS/
// Types (AudioSettings, AudioChannel) are defined in each platform file to be self-contained.

namespace KaraokeApp.Services;

/// <summary>Platform-specific microphone capture.</summary>
public interface IAudioCapture
{
    Task StartCaptureAsync(AudioSettings settings, Action<byte[]> onData, CancellationToken ct);
    Task StopCaptureAsync();
}

/// <summary>Platform-specific audio playback and mixing.</summary>
public interface IAudioPlayer
{
    void WriteMixBuffer(byte[] pcmData, AudioChannel channel, float volume);
    void PlayMusic(string filePath, float volume);
    void PauseMusic();
    void StopMusic();
    void StopAll();
    void UpdateVolumes(AudioSettings settings);
}

public enum AudioChannel { Mic1, Mic2, Music }

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
