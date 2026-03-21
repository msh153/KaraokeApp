using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;
using KaraokeApp.Models;

namespace KaraokeApp.Services;

public interface IBluetoothService
{
    bool IsBluetoothOn { get; }
    IReadOnlyList<BluetoothDevice> DiscoveredDevices { get; }
    BluetoothDevice? ConnectedSpeaker { get; }

    event EventHandler<BluetoothDevice> DeviceFound;
    event EventHandler<BluetoothDevice> DeviceConnected;
    event EventHandler<BluetoothDevice> DeviceDisconnected;
    event EventHandler<string> ErrorOccurred;

    Task<bool> StartScanAsync(CancellationToken ct = default);
    Task StopScanAsync();
    Task<bool> ConnectToSpeakerAsync(BluetoothDevice device);
    Task DisconnectSpeakerAsync();
}

public class BluetoothService : IBluetoothService
{
    private readonly IBluetoothLE _ble;
    private readonly IAdapter _adapter;
    private readonly List<BluetoothDevice> _discoveredDevices = new();
    private BluetoothDevice? _connectedSpeaker;
    private IDevice? _connectedBleDevice;

    public bool IsBluetoothOn => _ble.IsOn;
    public IReadOnlyList<BluetoothDevice> DiscoveredDevices => _discoveredDevices.AsReadOnly();
    public BluetoothDevice? ConnectedSpeaker => _connectedSpeaker;

    public event EventHandler<BluetoothDevice>? DeviceFound;
    public event EventHandler<BluetoothDevice>? DeviceConnected;
    public event EventHandler<BluetoothDevice>? DeviceDisconnected;
    public event EventHandler<string>? ErrorOccurred;

    public BluetoothService()
    {
        _ble = CrossBluetoothLE.Current;
        _adapter = CrossBluetoothLE.Current.Adapter;
        _adapter.ScanTimeout = 15000;

        _adapter.DeviceDiscovered += OnDeviceDiscovered;
        _adapter.DeviceConnected += OnDeviceConnected;
        _adapter.DeviceDisconnected += OnDeviceDisconnected;
        _adapter.DeviceConnectionLost += OnConnectionLost;
    }

    public async Task<bool> StartScanAsync(CancellationToken ct = default)
    {
        if (!_ble.IsOn)
        {
            ErrorOccurred?.Invoke(this, "Bluetooth вимкнений. Увімкніть Bluetooth і спробуйте знову.");
            return false;
        }

        _discoveredDevices.Clear();

        try
        {
            await _adapter.StartScanningForDevicesAsync(cancellationToken: ct);
            return true;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Помилка сканування: {ex.Message}");
            return false;
        }
    }

    public async Task StopScanAsync()
    {
        try
        {
            await _adapter.StopScanningForDevicesAsync();
        }
        catch { /* ignore */ }
    }

    public async Task<bool> ConnectToSpeakerAsync(BluetoothDevice device)
    {
        try
        {
            var bleDevice = _adapter.DiscoveredDevices
                .FirstOrDefault(d => d.Id.ToString() == device.Id);

            if (bleDevice == null)
            {
                ErrorOccurred?.Invoke(this, "Пристрій не знайдено. Виконайте повторне сканування.");
                return false;
            }

            await _adapter.ConnectToDeviceAsync(bleDevice);
            _connectedBleDevice = bleDevice;
            return true;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Помилка підключення: {ex.Message}");
            return false;
        }
    }

    public async Task DisconnectSpeakerAsync()
    {
        if (_connectedBleDevice != null)
        {
            try
            {
                await _adapter.DisconnectDeviceAsync(_connectedBleDevice);
            }
            catch { /* ignore */ }
            finally
            {
                _connectedBleDevice = null;
                _connectedSpeaker = null;
            }
        }
    }

    private void OnDeviceDiscovered(object? sender, DeviceEventArgs e)
    {
        var name = e.Device.Name ?? "Невідомий пристрій";
        if (string.IsNullOrWhiteSpace(name)) return;

        var device = new BluetoothDevice
        {
            Id = e.Device.Id.ToString(),
            Name = name,
            SignalStrength = e.Device.Rssi
        };

        if (!_discoveredDevices.Any(d => d.Id == device.Id))
        {
            _discoveredDevices.Add(device);
            DeviceFound?.Invoke(this, device);
        }
    }

    private void OnDeviceConnected(object? sender, DeviceEventArgs e)
    {
        _connectedSpeaker = _discoveredDevices.FirstOrDefault(d => d.Id == e.Device.Id.ToString());
        if (_connectedSpeaker != null)
        {
            _connectedSpeaker.IsConnected = true;
            DeviceConnected?.Invoke(this, _connectedSpeaker);
        }
    }

    private void OnDeviceDisconnected(object? sender, DeviceEventArgs e)
    {
        if (_connectedSpeaker != null)
        {
            _connectedSpeaker.IsConnected = false;
            DeviceDisconnected?.Invoke(this, _connectedSpeaker);
            _connectedSpeaker = null;
        }
    }

    private void OnConnectionLost(object? sender, DeviceErrorEventArgs e)
    {
        ErrorOccurred?.Invoke(this, $"З'єднання втрачено: {e.Device?.Name}");
        if (_connectedSpeaker != null)
        {
            _connectedSpeaker.IsConnected = false;
            DeviceDisconnected?.Invoke(this, _connectedSpeaker);
        }
    }
}
