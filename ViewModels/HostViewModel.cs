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
    private readonly IHeadsetService _headset;
    private bool _permissionsGranted;
    private CancellationTokenSource? _scanCts;
    private FileResult? _musicFileResult;

    [ObservableProperty] private string _statusText = "Готовий до підключення";
    [ObservableProperty] private string _speakerStatus = "🔴 JBL не підключений";
    [ObservableProperty] private string _clientStatus = "🔴 Клієнт не підключений";
    [ObservableProperty] private string _sessionCode = "";
    [ObservableProperty] private string? _selectedMusicPath;
    [ObservableProperty] private string _selectedMusicName = "Музика не обрана";
    [ObservableProperty] private bool _isSessionActive;
    [ObservableProperty] private bool _isMusicPlaying;
    [ObservableProperty] private bool _isSoloMode = true;
    [ObservableProperty] private double _musicVolume = 0.8;
    [ObservableProperty] private double _mic1Volume = 1.0;
    [ObservableProperty] private double _mic2Volume = 1.0;
    [ObservableProperty] private double _mic1Level;
    [ObservableProperty] private double _mic2Level;
    [ObservableProperty] private bool _musicMuted;
    [ObservableProperty] private bool _mic1Muted;
    [ObservableProperty] private bool _mic2Muted;
    [ObservableProperty] private List<BluetoothDevice> _availableDevices = new();
    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private string _audioOutputStatus = "🔈 Телефонний динамік";
    [ObservableProperty] private bool _isHeadsetConnected;

    public HostViewModel(IAudioService audio, SessionService session,
                         IBluetoothService bt, IHeadsetService headset)
    {
        _audio = audio;
        _session = session;
        _bt = bt;
        _headset = headset;

        _audio.Mic1LevelChanged += (_, v) => Mic1Level = v;
        _audio.Mic2LevelChanged += (_, v) => Mic2Level = v;

        _bt.DeviceConnected += (_, d) =>
            MainThread.BeginInvokeOnMainThread(() =>
                SpeakerStatus = $"🟢 {d.Name} підключений");
        _bt.DeviceDisconnected += (_, _) =>
            MainThread.BeginInvokeOnMainThread(() =>
                SpeakerStatus = "🔴 JBL відключений");
        _bt.DeviceFound += (_, d) =>
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (!AvailableDevices.Any(x => x.Id == d.Id))
                    AvailableDevices = new(AvailableDevices) { d };
            });
        _bt.ErrorOccurred += (_, msg) =>
            MainThread.BeginInvokeOnMainThread(() => StatusText = $"❌ {msg}");

        _session.ClientConnected += (_, ip) =>
            MainThread.BeginInvokeOnMainThread(() =>
            {
                ClientStatus = $"🟢 Клієнт: {ip}";
                StatusText = "Клієнт підключився!";
            });
        _session.ClientDisconnected += (_, _) =>
            MainThread.BeginInvokeOnMainThread(() =>
                ClientStatus = "🔴 Клієнт відключився");

        _headset.OutputChanged += (_, type) =>
            MainThread.BeginInvokeOnMainThread(() => UpdateAudioOutput(type));
        UpdateAudioOutput(_headset.GetCurrentOutput());
    }

    private void UpdateAudioOutput(AudioOutputType type)
    {
        IsHeadsetConnected = type != AudioOutputType.Speaker;
        AudioOutputStatus = type switch
        {
            AudioOutputType.WiredHeadset => "🎧 Дротова гарнітура",
            AudioOutputType.Bluetooth => "🔵 Bluetooth навушники",
            _ => "🔈 Телефонний динамік"
        };
    }

    public async Task InitAsync()
    {
        _permissionsGranted = await PermissionService.EnsureMicrophoneAsync();
        if (!_permissionsGranted)
        {
            StatusText = "⚠️ Потрібен дозвіл на мікрофон";
            return;
        }
        // Bluetooth — запитуємо тут щоб не питати при кожному скануванні
        await PermissionService.EnsureBluetoothAsync();

        // Застосувати початкові налаштування гучності
        ApplySettings();
    }

    [RelayCommand]
    private async Task ScanForSpeakerAsync()
    {
        try
        {
            if (!_bt.IsBluetoothOn)
            {
                StatusText = "⚠️ Увімкніть Bluetooth і спробуйте знову";
                return;
            }

            // Якщо вже сканує — зупинити
            if (IsScanning)
            {
                _scanCts?.Cancel();
                IsScanning = false; // ← обов'язково скинути
                StatusText = "Сканування зупинено";
                return;
            }

            if (!await PermissionService.EnsureBluetoothAsync())
            {
                StatusText = "⚠️ Потрібен дозвіл на Bluetooth";
                return;
            }

            _scanCts = new CancellationTokenSource();
            IsScanning = true;
            AvailableDevices = new();
            StatusText = "Сканування...";

            await _bt.StartScanAsync(_scanCts.Token);

            IsScanning = false;
            StatusText = AvailableDevices.Count > 0
                ? $"Знайдено {AvailableDevices.Count} пристроїв"
                : "Пристрої не знайдені";
        }
        catch (OperationCanceledException)
        {
            IsScanning = false;
            StatusText = "Сканування зупинено";
        }
        catch (Exception ex)
        {
            IsScanning = false;
            StatusText = $"❌ Помилка: {ex.Message}";
        }
        finally
        {
            _scanCts?.Dispose();
            _scanCts = null;
        }
    }

    [RelayCommand]
    private async Task ConnectToSpeakerAsync(BluetoothDevice device)
    {
        try
        {
            StatusText = $"Підключення до {device.Name}...";
            var ok = await _bt.ConnectToSpeakerAsync(device);
            StatusText = ok ? $"Підключено до {device.Name}!" : "Помилка підключення";
        }
        catch (Exception ex)
        {
            StatusText = $"❌ Помилка: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task StartSessionAsync()
    {
        try
        {
            if (!_permissionsGranted)
            {
                StatusText = "⚠️ Спочатку надайте дозвіл на мікрофон.";
                return;
            }

            if (IsSoloMode)
            {
                StatusText = "Запускання соло режиму...";
                await _audio.StartSoloModeAsync();
                IsSessionActive = true;
                StatusText = "Соло режим активний 🎤";
            }
            else
            {
                StatusText = "Запускання сесії...";
                await _session.StartHostSessionAsync();
                SessionCode = _session.CurrentSession?.SessionId ?? "";
                IsSessionActive = true;
                StatusText = "Сесія запущена. Поділіться кодом з клієнтом.";
                await _audio.StartHostModeAsync(_session.CurrentSession!);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"❌ Помилка: {ex.Message}";
        }
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
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Оберіть музичний файл",
                FileTypes = new FilePickerFileType(
                    new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.Android, new[] { "audio/*" } },
                    })
            });

            if (result == null) return;

            _musicFileResult = result;
            SelectedMusicName = Path.GetFileNameWithoutExtension(result.FileName);
            StatusText = $"🎵 Обрано: {SelectedMusicName}"; // ← підтвердження що обрано
        }
        catch (Exception ex)
        {
            StatusText = $"❌ Помилка вибору файлу: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ToggleMusicAsync()
    {
        if (_musicFileResult == null)
        {
            StatusText = "⚠️ Спочатку оберіть музику";
            return;
        }

        if (IsMusicPlaying)
        {
            _audio.PauseMusic();
            IsMusicPlaying = false;
            return;
        }

        // Копіюємо у кеш щоб уникнути проблем з content:// URI
        var cachedPath = Path.Combine(FileSystem.CacheDirectory, _musicFileResult.FileName);
        if (!File.Exists(cachedPath))
        {
            try
            {
                StatusText = "Завантаження файлу...";
                await using var src = await _musicFileResult.OpenReadAsync();
                await using var dst = File.Create(cachedPath);
                await src.CopyToAsync(dst);
            }
            catch (Exception ex)
            {
                StatusText = $"❌ Не вдалось відкрити файл: {ex.Message}";
                return;
            }
        }

        SelectedMusicPath = cachedPath;
        ApplySettings();
        _audio.PlayMusic(cachedPath);
        IsMusicPlaying = true;
        StatusText = $"▶ {SelectedMusicName}";
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
