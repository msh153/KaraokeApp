using KaraokeApp.ViewModels;

namespace KaraokeApp.Views;

public partial class ClientPage : ContentPage
{
    public ClientPage(ClientViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//main");
    }
}
