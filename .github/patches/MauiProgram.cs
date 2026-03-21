using CommunityToolkit.Maui;
namespace KaraokeApp;
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf",  "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });
        builder.Services.AddSingleton<IBluetoothService, BluetoothService>();
        builder.Services.AddSingleton<SessionService>();
#if ANDROID
        builder.Services.AddSingleton<IAudioCapture, KaraokeApp.Platforms.Android.AndroidAudioCapture>();
        builder.Services.AddSingleton<IAudioPlayer,  KaraokeApp.Platforms.Android.AndroidAudioPlayer>();
#endif
        builder.Services.AddSingleton<IAudioService, AudioService>();
        builder.Services.AddTransient<HostViewModel>();
        builder.Services.AddTransient<ClientViewModel>();
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<HostPage>();
        builder.Services.AddTransient<ClientPage>();
        return builder.Build();
    }
}