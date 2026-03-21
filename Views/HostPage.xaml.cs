using KaraokeApp.ViewModels;

namespace KaraokeApp.Views;

public partial class HostPage : ContentPage
{
    private readonly HostViewModel _vm;

    public HostPage(HostViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.InitAsync();
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await _vm.StopSessionCommand.ExecuteAsync(null);
        await Shell.Current.GoToAsync("//main");
    }
}
