using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KaraokeApp.Models;
using KaraokeApp.Services;

namespace KaraokeApp.ViewModels;

public partial class HostViewModel : ObservableObject
{
    private readonly IAudioService _audio;
    private readonly SessionService _session;
    private readonly IBluetoothService _bt;
    private bool _permissionsGranted;

    [ObservableProperty] private string _statusText = "Готовий до підключення";
    [ObservableProperty] private string _speakerStatus = "🔴 JBL не підключений";
    [ObservableProperty] private string _clientStatus = "🔴 Клієнт не підключений";
    [ObservableProperty] private string _sessionCode = "";
    [ObservableProperty] private string? _selectedMusicPath;
    [ObservableProperty] private string _selectedMusicName = "Музика не обрана";
    [ObservableProperty] private bool _isSessionActive;
    [ObservableProperty] private bool _isMusicPlaying;

    // Volume sliders (0..1)
    [ObservableProperty] private double _musicVolume = 0.8;
    [ObservableProperty] private double _mic1Volume = 1.0;
    [ObservableProperty] private double _mic2Volume = 1.0;

    // VU meters (0..1)
    [ObservableProperty] private double _mic1Level;
    [ObservableProperty] private double _mic2Level;

    // Mute toggles
    [ObservableProperty] private bool _musicMuted;
    [ObservableProperty] private bool _mic1Muted;
    [ObservableProperty] private bool _mic2Muted;

    [ObservableProperty] private List<BluetoothDevice> _availableDevices = new();
    [ObservableProperty] private bool _isScanning;

    public HostViewModel(IAudioService audio, SessionService session, IBluetoothService bt)
    {
        _audio = audio;
        _session = session;
        _bt = bt;

        _audio.Mic1LevelChanged += (_, v) => Mic1Level = v;
        _audio.Mic2LevelChanged += (_, v) => Mic2Level = v;
        _bt.DeviceConnected += (_, d) => SpeakerStatus = $"🟢 {d.Name} підключений";
        _bt.DeviceDisconnected += (_, _) => SpeakerStatus = "🔴 JBL відключений";
        _bt.DeviceFound += (_, d) => { if (!AvailableDevices.Any(x => x.Id == d.Id)) AvailableDevices = new(AvailableDevices) { d }; };
        _session.ClientConnected += (_, ip) => { ClientStatus = $"🟢 Клієнт: {ip}"; StatusText = "Клієнт підключився!"; };
        _session.ClientDisconnected += (_, _) => { ClientStatus = "🔴 Клієнт відключився"; };
    }

    public async Task InitAsync()
    {
        var result = await PermissionService.CheckAndRequestAllAsync();
        _permissionsGranted = result.AllGranted;

        if (!result.AllGranted)
        {
            StatusText = $"⚠️ Немає дозволів: {string.Join(", ", result.Denied)}. " +
                         "Надайте дозволи в Налаштуваннях.";
        }
    }

    [RelayCommand]
    private async Task ScanForSpeakerAsync()
    {
        if (!_permissionsGranted)
        {
            StatusText = "⚠️ Спочатку надайте всі необхідні дозволи.";
            return;
        }
        IsScanning = true;
        AvailableDevices = new();
        StatusText = "Сканування Bluetooth пристроїв...";
        await _bt.StartScanAsync();
        await Task.Delay(8000);
        await _bt.StopScanAsync();
        IsScanning = false;
        StatusText = AvailableDevices.Count > 0
            ? $"Знайдено {AvailableDevices.Count} пристроїв"
            : "Пристрої не знайдені";
    }

    [RelayCommand]
    private async Task ConnectToSpeakerAsync(BluetoothDevice device)
    {
        StatusText = $"Підключення до {device.Name}...";
        var ok = await _bt.ConnectToSpeakerAsync(device);
        StatusText = ok ? $"Підключено до {device.Name}!" : "Помилка підключення";
    }

    [RelayCommand]
    private async Task StartSessionAsync()
    {
        if (!_permissionsGranted)
        {
            StatusText = "⚠️ Спочатку надайте всі необхідні дозволи.";
            return;
        }
        await _session.StartHostSessionAsync();
        SessionCode = _session.CurrentSession?.SessionId ?? "";
        IsSessionActive = true;
        StatusText = "Сесія запущена. Поділіться кодом з клієнтом.";

        await _audio.StartHostModeAsync(_session.CurrentSession!);
    }

    [RelayCommand]
    private async Task StopSessionAsync()
    {
        await _audio.StopAsync();
        _session.StopSession();
        IsSessionActive = false;
        IsMusicPlaying = false;
        StatusText = "Сесію зупинено";
    }

    [RelayCommand]
    private async Task PickMusicAsync()
    {
        var path = await _audio.PickMusicFileAsync();
        if (path != null)
        {
            SelectedMusicPath = path;
            SelectedMusicName = Path.GetFileNameWithoutExtension(path);
        }
    }

    [RelayCommand]
    private void ToggleMusic()
    {
        if (SelectedMusicPath == null) return;

        if (IsMusicPlaying)
        {
            _audio.PauseMusic();
            IsMusicPlaying = false;
        }
        else
        {
            _audio.PlayMusic(SelectedMusicPath);
            IsMusicPlaying = true;
        }
    }

    partial void OnMusicVolumeChanged(double value) => ApplySettings();
    partial void OnMic1VolumeChanged(double value) => ApplySettings();
    partial void OnMic2VolumeChanged(double value) => ApplySettings();
    partial void OnMusicMutedChanged(bool value) => ApplySettings();
    partial void OnMic1MutedChanged(bool value) => ApplySettings();
    partial void OnMic2MutedChanged(bool value) => ApplySettings();

    private void ApplySettings()
    {
        _audio.UpdateSettings(new AudioSettings
        {
            MusicVolume = (float)MusicVolume,
            Mic1Volume = (float)Mic1Volume,
            Mic2Volume = (float)Mic2Volume,
            MusicMuted = MusicMuted,
            Mic1Muted = Mic1Muted,
            Mic2Muted = Mic2Muted
        });
    }
}
