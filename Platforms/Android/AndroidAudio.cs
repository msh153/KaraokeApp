using Android.Media;
using Android.OS;
using KaraokeApp.Services;

namespace KaraokeApp;

public class AndroidAudioCapture : IAudioCapture
{
    private AudioRecord? _audioRecord;
    private bool _isCapturing;

    public async Task StartCaptureAsync(
        AudioSettings settings,
        Action<byte[]> onData,
        CancellationToken ct)
    {
        var sampleRate = settings.SampleRate;
        var minBuf = AudioRecord.GetMinBufferSize(sampleRate, ChannelIn.Mono, Encoding.Pcm16bit);
        var bufferSize = Math.Max(settings.BufferSize, minBuf);

        _audioRecord = new AudioRecord(
            AudioSource.Mic,
            sampleRate,
            ChannelIn.Mono,
            Encoding.Pcm16bit,
            bufferSize);

        if (_audioRecord.State != State.Initialized)
            throw new InvalidOperationException("Не вдалось ініціалізувати мікрофон.");

        _audioRecord.StartRecording();
        _isCapturing = true;

        await Task.Run(async () =>
        {
            var buffer = new byte[bufferSize];
            while (_isCapturing && !ct.IsCancellationRequested)
            {
                var read = _audioRecord.Read(buffer, 0, buffer.Length);
                if (read > 0)
                {
                    var data = new byte[read];
                    Buffer.BlockCopy(buffer, 0, data, 0, read);
                    onData(data);
                }
                await Task.Delay(1, ct).ConfigureAwait(false);
            }
        }, ct).ConfigureAwait(false);
    }

    public Task StopCaptureAsync()
    {
        _isCapturing = false;
        try { _audioRecord?.Stop(); _audioRecord?.Release(); _audioRecord?.Dispose(); }
        catch { /* ignore */ }
        _audioRecord = null;
        return Task.CompletedTask;
    }
}

// ── AndroidAudioPlayer ────────────────────────────────────────────────────

public class AndroidAudioPlayer : IAudioPlayer
{
    private AudioTrack? _micMixTrack;
    private MediaPlayer? _mediaPlayer;
    private readonly object _lock = new();
    private bool _initialized;

    private void EnsureTrack(int sampleRate = 44100)
{
    if (_initialized) return;

    var minBuf = AudioTrack.GetMinBufferSize(
        sampleRate, ChannelOut.Mono, Encoding.Pcm16bit);

#pragma warning disable CA1416
    _micMixTrack = new AudioTrack.Builder()
        .SetAudioAttributes(
            new AudioAttributes.Builder()!
                .SetUsage(AudioUsageKind.Media)!
                .SetContentType(AudioContentType.Music)!
                .Build()!)
        .SetAudioFormat(
            new AudioFormat.Builder()!
                .SetSampleRate(sampleRate)!
                .SetEncoding(Encoding.Pcm16bit)!
                .SetChannelMask(ChannelOut.Mono)!
                .Build()!)
        .SetBufferSizeInBytes(minBuf * 4)
        .SetTransferMode(AudioTrackMode.Stream)
        .Build();
#pragma warning restore CA1416

    // ← явно запитати аудіо фокус перед відтворенням
    RequestAudioFocus();
    _micMixTrack!.Play();
    _initialized = true;
}

private void RequestAudioFocus()
{
    var audioManager = Android.Media.AudioManager.FromContext(Platform.AppContext);
    if (audioManager == null) return;

#pragma warning disable CA1422
    if (OperatingSystem.IsAndroidVersionAtLeast(26))
    {
        var request = new AudioFocusRequestClass.Builder(AudioFocus.Gain)
            .SetAudioAttributes(
                new AudioAttributes.Builder()!
                    .SetUsage(AudioUsageKind.Media)!
                    .SetContentType(AudioContentType.Music)!
                    .Build()!)
            .Build();
        audioManager.RequestAudioFocus(request);
    }
    else
    {
        audioManager.RequestAudioFocus(null, Android.Media.Stream.Music, AudioFocus.Gain);
    }
#pragma warning restore CA1422
}
    public void WriteMixBuffer(byte[] pcmData, AudioChannel channel, float volume)
    {
        lock (_lock)
        {
            EnsureTrack();
            if (_micMixTrack == null) return;
            var scaled = ApplyVolume(pcmData, volume);
            _micMixTrack.Write(scaled, 0, scaled.Length);
        }
    }

    private static byte[] ApplyVolume(byte[] pcm, float volume)
    {
        if (Math.Abs(volume - 1f) < 0.01f) return pcm;
        var result = new byte[pcm.Length];
        for (int i = 0; i < pcm.Length - 1; i += 2)
        {
            var sample = (short)(BitConverter.ToInt16(pcm, i) * volume);
            result[i] = (byte)(sample & 0xFF);
            result[i + 1] = (byte)((sample >> 8) & 0xFF);
        }
        return result;
    }

    public void PlayMusic(string filePath, float volume)
    {
        try
        {
            _mediaPlayer?.Stop();
            _mediaPlayer?.Release();
            _mediaPlayer = new MediaPlayer();

            if (filePath.StartsWith("content://"))
            {
                var uri = Android.Net.Uri.Parse(filePath);
                if (uri != null) // ← перевірка null
                    _mediaPlayer.SetDataSource(Platform.AppContext, uri);
                else
                {
                    System.Diagnostics.Debug.WriteLine("[AndroidAudioPlayer] Invalid URI");
                    return;
                }
            }
            else
            {
                _mediaPlayer.SetDataSource(filePath);
            }

            _mediaPlayer.SetAudioAttributes(
                new AudioAttributes.Builder()!
                    .SetUsage(AudioUsageKind.Media)!
                    .SetContentType(AudioContentType.Music)!
                    .Build()!);

            _mediaPlayer.SetVolume(volume, volume);
            _mediaPlayer.Prepare();
            _mediaPlayer.Start();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AndroidAudioPlayer] PlayMusic error: {ex}");
        }
    }
    public void PauseMusic()
    {
        if (_mediaPlayer?.IsPlaying == true) _mediaPlayer.Pause();
    }

    public void StopMusic()
    {
        _mediaPlayer?.Stop(); _mediaPlayer?.Release(); _mediaPlayer = null;
    }

    public void StopAll()
    {
        StopMusic();
        lock (_lock)
        {
            _micMixTrack?.Pause(); _micMixTrack?.Flush();
            _micMixTrack?.Release(); _micMixTrack = null;
            _initialized = false;
        }
    }

    public void UpdateVolumes(AudioSettings settings)
    {
        var vol = settings.MusicMuted ? 0f : settings.MusicVolume;
        _mediaPlayer?.SetVolume(vol, vol);
    }
}
