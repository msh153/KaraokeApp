namespace KaraokeApp.Views;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
    }

    private async void OnHostClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//host");
    }

    private async void OnClientClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//client");
    }
}
