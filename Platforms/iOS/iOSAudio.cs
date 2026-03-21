using AVFoundation;
using AudioToolbox;
using AudioUnit;
using KaraokeApp.Models;
using KaraokeApp.Services;

namespace KaraokeApp.Platforms.iOS;

public class iOSAudioCapture : IAudioCapture
{
    private AudioUnit.AudioUnit? _audioUnit;
    private Action<byte[]>? _onData;
    private bool _isCapturing;

    public async Task StartCaptureAsync(AudioSettings settings, Action<byte[]> onData, CancellationToken ct)
    {
        _onData = onData;

        // Configure AVAudioSession for simultaneous playback+record
        var session = AVAudioSession.SharedInstance();
        session.SetCategory(
            AVAudioSessionCategory.PlayAndRecord,
            AVAudioSessionCategoryOptions.DefaultToSpeaker |
            AVAudioSessionCategoryOptions.AllowBluetooth,
            out var err);
        session.SetPreferredSampleRate(settings.SampleRate, out _);
        session.SetActive(true, out _);

        // Set up RemoteIO AudioUnit
        var desc = AudioComponentDescription.CreateRemoteIO();
        var component = AudioComponent.FindComponent(desc)!;
        _audioUnit = AudioUnit.AudioUnit.CreateInstance(component)!;

        // Enable mic input (bus 1)
        _audioUnit.SetEnableIO(true, AudioUnitScopeType.Input, 1);
        // Enable speaker output (bus 0)  
        _audioUnit.SetEnableIO(true, AudioUnitScopeType.Output, 0);

        var format = new AudioStreamBasicDescription
        {
            SampleRate = settings.SampleRate,
            Format = AudioFormatType.LinearPCM,
            FormatFlags = AudioFormatFlags.IsSignedInteger | AudioFormatFlags.IsPacked,
            BitsPerChannel = 16,
            ChannelsPerFrame = 1,
            BytesPerFrame = 2,
            FramesPerPacket = 1,
            BytesPerPacket = 2
        };

        _audioUnit.SetFormat(format, AudioUnitScopeType.Output, 1);
        _audioUnit.SetFormat(format, AudioUnitScopeType.Input, 0);

        _audioUnit.SetInputCallback(InputCallback, AudioUnitScopeType.Global, 1);
        _audioUnit.Initialize();
        _audioUnit.Start();
        _isCapturing = true;

        // Keep alive until cancelled
        await Task.Delay(Timeout.Infinite, ct).ContinueWith(_ => { });
    }

    private AudioUnitStatus InputCallback(AudioUnitRenderActionFlags actionFlags,
        AudioTimeStamp timeStamp, uint busNumber, uint frames, AudioBuffers? data)
    {
        if (!_isCapturing || _audioUnit == null) return AudioUnitStatus.OK;

        var buffer = new AudioBuffers(1);
        buffer[0] = new AudioBuffer { NumberChannels = 1, DataByteSize = (int)(frames * 2) };
        buffer[0].Data = System.Runtime.InteropServices.Marshal.AllocHGlobal(buffer[0].DataByteSize);

        _audioUnit.Render(ref actionFlags, timeStamp, busNumber, frames, buffer);

        var bytes = new byte[buffer[0].DataByteSize];
        System.Runtime.InteropServices.Marshal.Copy(buffer[0].Data, bytes, 0, bytes.Length);
        System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer[0].Data);

        _onData?.Invoke(bytes);
        return AudioUnitStatus.OK;
    }

    public Task StopCaptureAsync()
    {
        _isCapturing = false;
        _audioUnit?.Stop();
        _audioUnit?.Dispose();
        _audioUnit = null;
        return Task.CompletedTask;
    }
}

public class iOSAudioPlayer : IAudioPlayer
{
    private AVAudioEngine? _engine;
    private AVAudioMixerNode? _mainMixer;
    private AVAudioPlayerNode? _mic1Node;
    private AVAudioPlayerNode? _mic2Node;
    private AVAudioPlayerNode? _musicNode;
    private AVAudioPlayer? _musicPlayer;
    private bool _initialized;

    private void EnsureEngine()
    {
        if (_initialized) return;
        _engine = new AVAudioEngine();
        _mainMixer = _engine.MainMixerNode;
        _mic1Node = new AVAudioPlayerNode();
        _mic2Node = new AVAudioPlayerNode();

        _engine.AttachNode(_mic1Node);
        _engine.AttachNode(_mic2Node);

        var format = new AVAudioFormat(44100, 1);
        _engine.Connect(_mic1Node, _mainMixer, format);
        _engine.Connect(_mic2Node, _mainMixer, format);

        _engine.StartAndReturnError(out _);
        _mic1Node.Play();
        _mic2Node.Play();
        _initialized = true;
    }

    public void WriteMixBuffer(byte[] pcmData, AudioChannel channel, float volume)
    {
        EnsureEngine();
        var node = channel == AudioChannel.Mic1 ? _mic1Node : _mic2Node;
        if (node == null) return;

        // Schedule PCM buffer on appropriate node
        var format = new AVAudioFormat(AVAudioCommonFormat.PCMFormatInt16, 44100, 1, false);
        var frameCount = (uint)(pcmData.Length / 2);
        var buffer = new AVAudioPcmBuffer(format, frameCount);

        if (buffer.Int16ChannelData != null)
        {
            System.Runtime.InteropServices.Marshal.Copy(pcmData, 0,
                buffer.Int16ChannelData[0], pcmData.Length);
        }
        buffer.FrameLength = frameCount;
        node.Volume = volume;
        node.ScheduleBuffer(buffer, null);
    }

    public void PlayMusic(string filePath, float volume)
    {
        _musicPlayer?.Stop();
        _musicPlayer?.Dispose();
        _musicPlayer = new AVAudioPlayer(new Foundation.NSUrl(filePath, false), out _);
        _musicPlayer.Volume = volume;
        _musicPlayer.NumberOfLoops = 0;
        _musicPlayer.PrepareToPlay();
        _musicPlayer.Play();
    }

    public void PauseMusic() => _musicPlayer?.Pause();
    public void StopMusic()
    {
        _musicPlayer?.Stop();
        _musicPlayer?.Dispose();
        _musicPlayer = null;
    }

    public void StopAll()
    {
        StopMusic();
        _engine?.Stop();
        _initialized = false;
    }

    public void UpdateVolumes(AudioSettings settings)
    {
        if (_mic1Node != null) _mic1Node.Volume = settings.Mic1Muted ? 0f : settings.Mic1Volume;
        if (_mic2Node != null) _mic2Node.Volume = settings.Mic2Muted ? 0f : settings.Mic2Volume;
        if (_musicPlayer != null) _musicPlayer.Volume = settings.MusicMuted ? 0f : settings.MusicVolume;
    }
}
