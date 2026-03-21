using KaraokeApp.Models;

namespace KaraokeApp.Services;

/// <summary>
/// Platform-specific microphone capture.
/// Implemented separately for Android and iOS.
/// </summary>
public interface IAudioCapture
{
    Task StartCaptureAsync(AudioSettings settings, Action<byte[]> onData, CancellationToken ct);
    Task StopCaptureAsync();
}

/// <summary>
/// Platform-specific audio playback and mixing.
/// Implemented separately for Android and iOS.
/// </summary>
public interface IAudioPlayer
{
    /// <summary>Write PCM data for a given channel into the mix buffer.</summary>
    void WriteMixBuffer(byte[] pcmData, AudioChannel channel, float volume);
    void PlayMusic(string filePath, float volume);
    void PauseMusic();
    void StopMusic();
    void StopAll();
    void UpdateVolumes(AudioSettings settings);
}
