using Android.App;
using Android.Content.PM;
using Android.OS;
using Plugin.BLE;

namespace KaraokeApp;

[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
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
        RequestRuntimePermissions();
    }

    private void RequestRuntimePermissions()
    {
        // Всі дозволи що потрібні — збираємо список залежно від API
        var needed = new List<string>();

#pragma warning disable CA1416
        // ── Мікрофон ─────────────────────────────────────────────────────
        if (CheckSelfPermission(global::Android.Manifest.Permission.RecordAudio)
            != Permission.Granted)
            needed.Add(global::Android.Manifest.Permission.RecordAudio);

        // ── Bluetooth ─────────────────────────────────────────────────────
        if (Build.VERSION.SdkInt >= BuildVersionCodes.S) // API 31+
        {
            if (CheckSelfPermission(global::Android.Manifest.Permission.BluetoothScan)
                != Permission.Granted)
                needed.Add(global::Android.Manifest.Permission.BluetoothScan);

            if (CheckSelfPermission(global::Android.Manifest.Permission.BluetoothConnect)
                != Permission.Granted)
                needed.Add(global::Android.Manifest.Permission.BluetoothConnect);
        }
        else
        {
            if (CheckSelfPermission(global::Android.Manifest.Permission.Bluetooth)
                != Permission.Granted)
                needed.Add(global::Android.Manifest.Permission.Bluetooth);

            if (CheckSelfPermission(global::Android.Manifest.Permission.BluetoothAdmin)
                != Permission.Granted)
                needed.Add(global::Android.Manifest.Permission.BluetoothAdmin);
        }

        // ── Мережа / Wi-Fi ────────────────────────────────────────────────
        if (CheckSelfPermission(global::Android.Manifest.Permission.AccessWifiState)
            != Permission.Granted)
            needed.Add(global::Android.Manifest.Permission.AccessWifiState);

        // ── Медіа / сховище ───────────────────────────────────────────────
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu) // API 33+
        {
            if (CheckSelfPermission(global::Android.Manifest.Permission.ReadMediaAudio)
                != Permission.Granted)
                needed.Add(global::Android.Manifest.Permission.ReadMediaAudio);
        }
        else
        {
            if (CheckSelfPermission(global::Android.Manifest.Permission.ReadExternalStorage)
                != Permission.Granted)
                needed.Add(global::Android.Manifest.Permission.ReadExternalStorage);
        }

        if (needed.Count > 0)
            RequestPermissions(needed.ToArray(), PermissionRequestCode);
#pragma warning restore CA1416
    }

    public override void OnRequestPermissionsResult(
        int requestCode, string[] permissions, Permission[] grantResults)
    {
#pragma warning disable CA1416
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
#pragma warning restore CA1416
    }
}