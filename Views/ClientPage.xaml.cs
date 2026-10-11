using KaraokeApp.ViewModels;

namespace KaraokeApp.Views;

public partial class ClientPage : ContentPage
{
    private readonly ClientViewModel _vm;

    public ClientPage(ClientViewModel vm)
    {
        try
        {
            InitializeComponent();
            _vm = vm;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ClientPage] Constructor error: {ex}");
            throw;
        }

        _vm.PropertyChanged += (_, e) =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                switch (e.PropertyName)
                {
                    case nameof(_vm.HostStatus):
                        LblHostStatus.Text = _vm.HostStatus; break;
                    case nameof(_vm.StatusText):
                        LblStatus.Text = _vm.StatusText; break;
                    case nameof(_vm.IsConnected):
                        FrameConnect.IsVisible = !_vm.IsConnected;
                        BtnDisconnect.IsVisible = _vm.IsConnected; break;
                    case nameof(_vm.IsMicMuted):
                        BtnMic.Text = _vm.IsMicMuted ? "🔇" : "🎤";
                        LblMicState.Text = _vm.IsMicMuted ? "МІК ВИМКНЕНО" : "МІК АКТИВНИЙ";
                        LblMicState.TextColor = _vm.IsMicMuted
                            ? Color.FromArgb("#FF5252")
                            : Color.FromArgb("#00E676");
                        break;
                    case nameof(_vm.MicLevel):
                        ProgMicLevel.Progress = _vm.MicLevel; break;
                    case nameof(_vm.IsSearching):
                        Spinner.IsVisible = _vm.IsSearching;
                        Spinner.IsRunning = _vm.IsSearching; break;
                }
            });
        };
    }

    private async void OnFindClicked(object s, EventArgs e)
    {
        try
        {
            await _vm.FindAndConnectCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ClientPage] FindClicked error: {ex}");
            await DisplayAlert("Помилка", $"Помилка пошуку: {ex.Message}", "OK");
        }
    }

    private async void OnConnectByCodeClicked(object s, EventArgs e)
    {
        try
        {
            _vm.SessionCode = EntryCode.Text ?? "";
            await _vm.ConnectWithCodeCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ClientPage] ConnectByCodeClicked error: {ex}");
            await DisplayAlert("Помилка", $"Помилка підключення: {ex.Message}", "OK");
        }
    }

    private void OnMicToggleClicked(object s, EventArgs e)
        => _vm.ToggleMicCommand.Execute(null);

    private void OnMicVolumeChanged(object s, ValueChangedEventArgs e)
        => _vm.MicVolume = e.NewValue;

    private async void OnDisconnectClicked(object s, EventArgs e)
    {
        try
        {
            await _vm.DisconnectCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ClientPage] DisconnectClicked error: {ex}");
            await DisplayAlert("Помилка", $"Помилка відключення: {ex.Message}", "OK");
        }
    }

    private async void OnBackClicked(object s, EventArgs e)
    {
        try
        {
            await Shell.Current.GoToAsync("//main");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ClientPage] BackClicked error: {ex}");
        }
    }
}