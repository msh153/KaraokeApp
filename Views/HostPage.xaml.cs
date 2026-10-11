using KaraokeApp.ViewModels;

namespace KaraokeApp.Views;

public partial class HostPage : ContentPage
{
    private readonly HostViewModel _vm;

    public HostPage(HostViewModel vm)
    {
        try
        {
            InitializeComponent();
            _vm = vm;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HostPage] Constructor error: {ex}");
            throw;
        }

        _vm.PropertyChanged += (_, e) =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                switch (e.PropertyName)
                {
                    case nameof(_vm.StatusText):
                        LblStatus.Text = _vm.StatusText; break;
                    case nameof(_vm.SpeakerStatus):
                        LblSpeaker.Text = _vm.SpeakerStatus;
                        var isConnected = _vm.SpeakerStatus.StartsWith("🟢");
                        GridConnectedDevice.IsVisible = isConnected;
                        if (isConnected)
                            LblConnectedDeviceName.Text = _vm.SpeakerStatus
                                .Replace("🟢 ", "").Replace(" підключений", "");
                        break;
                    case nameof(_vm.ClientStatus):
                        LblClient.Text = _vm.ClientStatus; break;
                    case nameof(_vm.SessionCode):
                        LblSessionCode.Text = _vm.SessionCode;
                        FrameSessionCode.IsVisible = !string.IsNullOrEmpty(_vm.SessionCode);
                        break;
                    case nameof(_vm.IsSessionActive):
                        BtnSession.Text = _vm.IsSessionActive
                            ? "⏹ Зупинити сесію" : "▶ Запустити сесію";
                        BtnSession.BackgroundColor = _vm.IsSessionActive
                            ? Color.FromArgb("#FF3B30") : Color.FromArgb("#6C3CE1");
                        break;
                    case nameof(_vm.IsMusicPlaying):
                        BtnPlayPause.Text = _vm.IsMusicPlaying ? "⏸" : "▶"; break;
                    case nameof(_vm.Mic1Level):
                        ProgMic1.Progress = _vm.Mic1Level; break;
                    case nameof(_vm.Mic2Level):
                        ProgMic2.Progress = _vm.Mic2Level; break;
                    case nameof(_vm.SelectedMusicName):
                        LblMusicName.Text = _vm.SelectedMusicName; break;
                    case nameof(_vm.IsScanning):
                        BtnScan.Text = _vm.IsScanning
                            ? "⏹ Зупинити" : "🔄 Перевірити підключення";
                        break;
                    case nameof(_vm.IsSoloMode):
                        SwitchSoloMode.IsToggled = _vm.IsSoloMode;
                        LblModeTitle.Text = _vm.IsSoloMode
                            ? "Соло режим" : "Мережева сесія (Wi-Fi)";
                        LblModeDesc.Text = _vm.IsSoloMode
                            ? "Один телефон, без Wi-Fi"
                            : "Два телефони через одну Wi-Fi мережу";
                        break;
                    case nameof(_vm.AudioOutputStatus):
                        LblAudioOutput.Text = _vm.AudioOutputStatus; break;
                    case nameof(_vm.IsHeadsetConnected):
                        LblAudioWarning.IsVisible = !_vm.IsHeadsetConnected; break;
                }
            });
        };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try { await _vm.InitAsync(); }
            catch (Exception ex)
            {
                await DisplayAlert("Помилка", $"Помилка ініціалізації: {ex.Message}", "OK");
            }
        });
    }

    private async void OnScanClicked(object s, EventArgs e)
    {
        try { await _vm.ScanForSpeakerCommand.ExecuteAsync(null); }
        catch (Exception ex)
        {
            await DisplayAlert("Помилка", $"Помилка: {ex.Message}", "OK");
        }
    }

    private async void OnSessionClicked(object s, EventArgs e)
    {
        try
        {
            if (_vm.IsSessionActive)
                await _vm.StopSessionCommand.ExecuteAsync(null);
            else
                await _vm.StartSessionCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Помилка", $"Помилка сесії: {ex.Message}", "OK");
        }
    }

    private async void OnPickMusicClicked(object s, EventArgs e)
    {
        try { await _vm.PickMusicCommand.ExecuteAsync(null); }
        catch (Exception ex)
        {
            await DisplayAlert("Помилка", $"Помилка вибору музики: {ex.Message}", "OK");
        }
    }

    // ← async void щоб await спрацював
    private async void OnPlayPauseClicked(object s, EventArgs e)
    {
        try { await _vm.ToggleMusicCommand.ExecuteAsync(null); }
        catch (Exception ex)
        {
            await DisplayAlert("Помилка", $"Помилка відтворення: {ex.Message}", "OK");
        }
    }

    private void OnMusicVolumeChanged(object s, ValueChangedEventArgs e)
        => _vm.MusicVolume = e.NewValue;

    private void OnMic1VolumeChanged(object s, ValueChangedEventArgs e)
        => _vm.Mic1Volume = e.NewValue;

    private void OnMic2VolumeChanged(object s, ValueChangedEventArgs e)
        => _vm.Mic2Volume = e.NewValue;

    private void OnSoloModeToggled(object s, ToggledEventArgs e)
        => _vm.IsSoloMode = e.Value;

    private async void OnBackClicked(object s, EventArgs e)
    {
        try
        {
            if (_vm.IsSessionActive)
                await _vm.StopSessionCommand.ExecuteAsync(null);
            await Shell.Current.GoToAsync("//main");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HostPage] BackClicked error: {ex}");
        }
    }
}