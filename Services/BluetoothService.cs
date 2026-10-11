using Android.Bluetooth;
using Android.Content;
using Android.Media;
using KaraokeApp.Models;

using AppBtDevice = KaraokeApp.Models.BluetoothDevice;

namespace KaraokeApp.Services;

public interface IBluetoothService
{
    bool IsBluetoothOn { get; }
    IReadOnlyList<AppBtDevice> DiscoveredDevices { get; }
    AppBtDevice? ConnectedSpeaker { get; }

    event EventHandler<AppBtDevice> DeviceFound;
    event EventHandler<AppBtDevice> DeviceConnected;
    event EventHandler<AppBtDevice> DeviceDisconnected;
    event EventHandler<string> ErrorOccurred;

    Task<bool> StartScanAsync(CancellationToken ct = default);
    Task StopScanAsync();
    Task<bool> ConnectToSpeakerAsync(AppBtDevice device);
    Task DisconnectSpeakerAsync();
}

public class BluetoothService : IBluetoothService
{
    private readonly AudioManager _audioManager;
    private AppBtDevice? _connectedSpeaker;

#pragma warning disable CA1422
    public bool IsBluetoothOn =>
        BluetoothAdapter.DefaultAdapter?.IsEnabled ?? false;
#pragma warning restore CA1422

    public IReadOnlyList<AppBtDevice> DiscoveredDevices =>
        _connectedSpeaker != null
            ? new List<AppBtDevice> { _connectedSpeaker }
            : new List<AppBtDevice>();

    public AppBtDevice? ConnectedSpeaker => _connectedSpeaker;

    public event EventHandler<AppBtDevice>? DeviceFound;
    public event EventHandler<AppBtDevice>? DeviceConnected;
    public event EventHandler<AppBtDevice>? DeviceDisconnected;
    public event EventHandler<string>? ErrorOccurred = delegate { };

    public BluetoothService()
    {
        _audioManager = (AudioManager)
            Platform.AppContext.GetSystemService(Context.AudioService)!;

        var filter = new IntentFilter();
        filter.AddAction(BluetoothA2dp.ActionConnectionStateChanged);
        Platform.AppContext.RegisterReceiver(new A2dpReceiver(this), filter);
    }

    public Task<bool> StartScanAsync(CancellationToken ct = default)
    {
        RefreshConnectedDevice();
        return Task.FromResult(true);
    }

    public Task StopScanAsync() => Task.CompletedTask;

    public Task<bool> ConnectToSpeakerAsync(AppBtDevice device)
        => Task.FromResult(true);

    public Task DisconnectSpeakerAsync() => Task.CompletedTask;

    public void RefreshConnectedDevice()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(23)) return;

        var devices = _audioManager.GetDevices(GetDevicesTargets.Outputs);
        if (devices == null) return;

        foreach (var d in devices)
        {
            if (d.Type == AudioDeviceType.BluetoothA2dp)
            {
                var dev = new AppBtDevice
                {
                    Id = d.Id.ToString(),
                    Name = d.ProductName?.ToString() ?? "Bluetooth Speaker",
                    IsConnected = true
                };
                _connectedSpeaker = dev;
                DeviceFound?.Invoke(this, dev);
                DeviceConnected?.Invoke(this, dev);
                return;
            }
        }

        if (_connectedSpeaker != null)
        {
            DeviceDisconnected?.Invoke(this, _connectedSpeaker);
            _connectedSpeaker = null;
        }
    }

    private class A2dpReceiver : BroadcastReceiver
    {
        private readonly BluetoothService _parent;
        public A2dpReceiver(BluetoothService parent) => _parent = parent;

        private const int StateConnected    = 2;
        private const int StateDisconnected = 0;

        public override void OnReceive(Context? context, Intent? intent)
        {
            if (intent?.Action != BluetoothA2dp.ActionConnectionStateChanged)
                return;

            var state = intent.GetIntExtra("android.bluetooth.profile.extra.STATE", StateDisconnected);

            if (state == StateConnected || state == StateDisconnected)
                MainThread.BeginInvokeOnMainThread(() =>
                    _parent.RefreshConnectedDevice());
        }
    }
}
