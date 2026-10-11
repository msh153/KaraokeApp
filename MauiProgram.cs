using CommunityToolkit.Maui;
using KaraokeApp.Services;
using KaraokeApp.ViewModels;
using KaraokeApp.Views;
using KaraokeApp.Platforms.Android;

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
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        Register(builder.Services);

        var app = builder.Build();

#if DEBUG
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"[AppDomain] Unhandled exception: {e.ExceptionObject}");
        };

        TaskScheduler.UnobservedTaskException += (sender, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"[TaskScheduler] Unobserved exception: {e.Exception}");
            e.SetObserved();
        };
#endif

        return app;
    }

    private static void Register(IServiceCollection s)
    {
        // ── Core ──────────────────────────────────────────────────────────
        s.AddSingleton<IBluetoothService, BluetoothService>();
        s.AddSingleton<SessionService>();

        // ── Platform Audio ────────────────────────────────────────────────
#if ANDROID
        s.AddSingleton<IAudioCapture, KaraokeApp.AndroidAudioCapture>();
        s.AddSingleton<IAudioPlayer, KaraokeApp.AndroidAudioPlayer>();
        s.AddSingleton<IHeadsetService, HeadsetService>();
#elif IOS
        s.AddSingleton<IAudioCapture, KaraokeApp.iOSAudioCapture>();
        s.AddSingleton<IAudioPlayer, KaraokeApp.iOSAudioPlayer>();
#elif WINDOWS
        s.AddSingleton<IAudioCapture>(_ => new WindowsAudioStub());
        s.AddSingleton<IAudioPlayer>(_ => new WindowsAudioStub());
#endif

        s.AddSingleton<IAudioService, AudioService>();

        // ── ViewModels ────────────────────────────────────────────────────
        s.AddTransient<HostViewModel>();
        s.AddTransient<ClientViewModel>();

        // ── Pages ─────────────────────────────────────────────────────────
        s.AddTransient<MainPage>();
        s.AddTransient<HostPage>();
        s.AddTransient<ClientPage>();
    }
}

#if WINDOWS
file sealed class WindowsAudioStub : IAudioCapture, IAudioPlayer
{
    // IAudioCapture
    public Task StartCaptureAsync(
        KaraokeApp.Services.AudioSettings settings,
        Action<byte[]> onData,
        CancellationToken ct) => Task.CompletedTask;

    public Task StopCaptureAsync() => Task.CompletedTask;

    // IAudioPlayer
    public void WriteMixBuffer(byte[] pcmData,
        KaraokeApp.Services.AudioChannel channel, float volume)
    { }
    public void PlayMusic(string filePath, float volume) { }
    public void PauseMusic() { }
    public void StopMusic() { }
    public void StopAll() { }
    public void UpdateVolumes(KaraokeApp.Services.AudioSettings settings) { }
}
#endif
