using AVFoundation;
using Foundation;
using UIKit;

namespace KaraokeApp;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
    {
        // Configure AVAudioSession early so BT audio routes are ready
        var session = AVAudioSession.SharedInstance();
        session.SetCategory(
            AVAudioSessionCategory.PlayAndRecord,
            AVAudioSessionCategoryOptions.DefaultToSpeaker |
            AVAudioSessionCategoryOptions.AllowBluetooth |
            AVAudioSessionCategoryOptions.AllowBluetoothA2DP,
            out _);
        session.SetActive(true, out _);

        return base.FinishedLaunching(application, launchOptions);
    }
}
