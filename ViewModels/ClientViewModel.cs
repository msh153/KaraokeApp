using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KaraokeApp.Models;
using KaraokeApp.Services;

namespace KaraokeApp.ViewModels;

public partial class ClientViewModel : ObservableObject
{
    private readonly IAudioService _audio;
    private readonly SessionService _session;

    [ObservableProperty] private string _statusText = "Пошук хоста...";
    [ObservableProperty] private string _hostStatus = "🔴 Хост не знайдений";
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private bool _isMicActive;
    [ObservableProperty] private bool _isMicMuted;
    [ObservableProperty] private double _micVolume = 1.0;
    [ObservableProperty] private double _micLevel;
    [ObservableProperty] private string _sessionCode = "";
    [ObservableProperty] private bool _isSearching;

    public ClientViewModel(IAudioService audio, SessionService session)
    {
        _audio = audio;
        _session = session;

        _audio.Mic1LevelChanged += (_, v) => MicLevel = v;
        _session.ClientDisconnected += (_, _) =>
        {
            IsConnected = false;
            HostStatus = "🔴 Хост відключився";
            StatusText = "З'єднання втрачено";
        };
    }

    [RelayCommand]
    private async Task FindAndConnectAsync()
    {
        IsSearching = true;
        StatusText = "Пошук хоста у мережі...";

        var session = await _session.DiscoverHostAsync(TimeSpan.FromSeconds(15));
        if (session == null)
        {
            StatusText = "Хост не знайдений. Переконайтесь, що обидва пристрої в одній Wi-Fi мережі.";
            IsSearching = false;
            return;
        }

        var joined = await _session.JoinSessionAsync(session);
        IsSearching = false;

        if (joined)
        {
            HostStatus = $"🟢 Хост: {session.HostIp}";
            IsConnected = true;
            StatusText = "Підключено! Мікрофон готовий.";
            await _audio.StartClientModeAsync(session);
            IsMicActive = true;
        }
        else
        {
            StatusText = "Не вдалось підключитись до хоста.";
        }
    }

    [RelayCommand]
    private async Task ConnectWithCodeAsync()
    {
        if (string.IsNullOrWhiteSpace(SessionCode)) return;

        IsSearching = true;
        StatusText = "Пошук хоста за кодом...";

        var session = await _session.DiscoverHostAsync(TimeSpan.FromSeconds(15));

        if (session?.SessionId?.Equals(SessionCode, StringComparison.OrdinalIgnoreCase) != true)
        {
            StatusText = "Сесія з цим кодом не знайдена.";
            IsSearching = false;
            return;
        }

        var joined = await _session.JoinSessionAsync(session);
        IsSearching = false;

        if (joined)
        {
            HostStatus = $"🟢 Хост знайдений ({session.SessionId})";
            IsConnected = true;
            StatusText = "Підключено! Починайте співати!";
            await _audio.StartClientModeAsync(session);
            IsMicActive = true;
        }
    }

    [RelayCommand]
    private void ToggleMic()
    {
        IsMicMuted = !IsMicMuted;
        _audio.UpdateSettings(new AudioSettings
        {
            Mic1Muted = IsMicMuted,
            Mic1Volume = (float)MicVolume
        });
        StatusText = IsMicMuted ? "🔇 Мікрофон вимкнено" : "🎤 Мікрофон увімкнено";
    }

    [RelayCommand]
    private async Task DisconnectAsync()
    {
        await _audio.StopAsync();
        _session.StopSession();
        IsConnected = false;
        IsMicActive = false;
        HostStatus = "🔴 Відключено";
        StatusText = "Відключено від сесії";
    }

    partial void OnMicVolumeChanged(double value)
    {
        _audio.UpdateSettings(new AudioSettings
        {
            Mic1Volume = (float)value,
            Mic1Muted = IsMicMuted
        });
    }
}
