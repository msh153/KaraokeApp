using Android.Bluetooth;
using Android.Content;
using Android.Media;
using KaraokeApp.Services;
using AndroidBluetooth = Android.Bluetooth;

namespace KaraokeApp.Platforms.Android;

public class HeadsetService : BroadcastReceiver, IHeadsetService
{
    private readonly AudioManager _audioManager;
    public event EventHandler<AudioOutputType>? OutputChanged;

    public HeadsetService()
    {
        _audioManager = Platform.AppContext
            .GetSystemService(Context.AudioService) as AudioManager
            ?? throw new InvalidOperationException("AudioManager недоступний");

        var filter = new IntentFilter();
        filter.AddAction(AudioManager.ActionHeadsetPlug);
        filter.AddAction(BluetoothA2dp.ActionConnectionStateChanged);
        Platform.AppContext.RegisterReceiver(this, filter);
    }

    public AudioOutputType GetCurrentOutput()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(23))
            return AudioOutputType.Speaker;

        var devices = _audioManager.GetDevices(GetDevicesTargets.Outputs);
        if (devices != null)
        {
            foreach (var d in devices)
            {
                if (d.Type == AudioDeviceType.BluetoothA2dp)
                    return AudioOutputType.Bluetooth;
                if (d.Type == AudioDeviceType.WiredHeadset ||
                    d.Type == AudioDeviceType.WiredHeadphones)
                    return AudioOutputType.WiredHeadset;
            }
        }

        // Fallback — перевірити через BluetoothAdapter
        #pragma warning disable CA1422
            var adapter = BluetoothAdapter.DefaultAdapter;
        #pragma warning restore CA1422
            if (adapter?.IsEnabled == true)
            {
                var bonded = adapter.BondedDevices;
                if (bonded != null)
                {
                    foreach (var b in bonded)
                    {
                        // Якщо є спарений пристрій типу аудіо — вважати підключеним
                        if (b.BluetoothClass?.MajorDeviceClass ==
                            AndroidBluetooth.MajorDeviceClass.AudioVideo)
                            return AudioOutputType.Bluetooth;
                    }
                }
            }

            return AudioOutputType.Speaker;
        }

    public override void OnReceive(Context? context, Intent? intent)
    {
        OutputChanged?.Invoke(this, GetCurrentOutput());
    }
}
