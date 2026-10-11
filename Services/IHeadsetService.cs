namespace KaraokeApp.Services;

public enum AudioOutputType { Speaker, WiredHeadset, Bluetooth }

public interface IHeadsetService
{
    AudioOutputType GetCurrentOutput();
    event EventHandler<AudioOutputType> OutputChanged;
}