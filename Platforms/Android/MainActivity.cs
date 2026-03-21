using Android.App;
using Android.Content.PM;
using Android.OS;
using Plugin.BLE;

namespace KaraokeApp.Platforms.Android;

[Activity(
    Theme = "@style/Maui.MainTheme.NoActionBar",
    MainLauncher = false,
    ConfigurationChanges =
        ConfigChanges.ScreenSize |
        ConfigChanges.Orientation |
        ConfigChanges.UiMode |
        ConfigChanges.ScreenLayout |
        ConfigChanges.SmallestScreenSize |
        ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    private const int PermissionRequestCode = 1001;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        RequestAllPermissions();
    }

    private void RequestAllPermissions()
    {
        var permissions = new List<string>
        {
            global::Android.Manifest.Permission.RecordAudio,
            global::Android.Manifest.Permission.ModifyAudioSettings,
            global::Android.Manifest.Permission.Internet,
            global::Android.Manifest.Permission.AccessNetworkState,
            global::Android.Manifest.Permission.AccessWifiState,
        };

        // Bluetooth permissions differ by API level
        if (Build.VERSION.SdkInt >= BuildVersionCodes.S)
        {
            permissions.Add(global::Android.Manifest.Permission.BluetoothConnect);
            permissions.Add(global::Android.Manifest.Permission.BluetoothScan);
        }
        else
        {
            permissions.Add(global::Android.Manifest.Permission.Bluetooth);
            permissions.Add(global::Android.Manifest.Permission.BluetoothAdmin);
        }

        // Storage / media
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
        {
            permissions.Add(global::Android.Manifest.Permission.ReadMediaAudio);
        }
        else
        {
            permissions.Add(global::Android.Manifest.Permission.ReadExternalStorage);
        }

        var needed = permissions
            .Where(p => CheckSelfPermission(p) != Permission.Granted)
            .ToArray();

        if (needed.Length > 0)
            RequestPermissions(needed, PermissionRequestCode);
    }

    public override void OnRequestPermissionsResult(
        int requestCode,
        string[] permissions,
        Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        Plugin.BLE.Abstractions.Contracts.IAdapter? _ = null; // keep BLE alive
    }

    // Forward BLE scan results
    protected override void OnActivityResult(int requestCode, Result resultCode,
        global::Android.Content.Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
    }
}
