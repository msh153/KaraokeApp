using Android.Media;
using KaraokeApp.Platforms.iOS;
using KaraokeApp.Services;

namespace KaraokeApp.Platforms.Android;

public class AndroidAudioCapture : IAudioCapture
{
    private AudioRecord? _audioRecord;
    private bool _isCapturing;

    public async Task StartCaptureAsync(
        AudioSettings settings,
        Action<byte[]> onData,
        CancellationToken ct)
    {
        var channelConfig = ChannelIn.Mono;
        var audioFormat = Encoding.Pcm16bit;
        var sampleRate = settings.SampleRate;

        var minBuf = AudioRecord.GetMinBufferSize(sampleRate, channelConfig, audioFormat);
        var bufferSize = Math.Max(settings.BufferSize, minBuf);

        _audioRecord = new AudioRecord(
            AudioSource.Mic,
            sampleRate,
            channelConfig,
            audioFormat,
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

public class AndroidAudioPlayer : IAudioPlayer
{
    private AudioTrack? _micMixTrack;
    private MediaPlayer? _mediaPlayer;
    private readonly object _lock = new();
    private bool _initialized;

    private void EnsureTrack(int sampleRate = 44100)
    {
        if (_initialized) return;
        var minBuf = AudioTrack.GetMinBufferSize(sampleRate, ChannelOut.Mono, Encoding.Pcm16bit);
        _micMixTrack = new AudioTrack.Builder()
            .SetAudioAttributes(new AudioAttributes.Builder()
                .SetUsage(AudioUsageKind.Media)
                .SetContentType(AudioContentType.Music)
                .Build())
            .SetAudioFormat(new AudioFormat.Builder()
                .SetSampleRate(sampleRate)
                .SetEncoding(Encoding.Pcm16bit)
                .SetChannelMask(ChannelOut.Mono)
                .Build())
            .SetBufferSizeInBytes(minBuf * 4)
            .SetTransferMode(AudioTrackMode.Stream)
            .Build();
        _micMixTrack.Play();
        _initialized = true;
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
        _mediaPlayer?.Stop(); _mediaPlayer?.Release();
        _mediaPlayer = new MediaPlayer();
        _mediaPlayer.SetDataSource(filePath);
        _mediaPlayer.SetVolume(volume, volume);
        _mediaPlayer.Prepare();
        _mediaPlayer.Start();
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