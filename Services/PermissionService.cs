namespace KaraokeApp.Services;

/// <summary>
/// Wraps MAUI's permission API with clear error messages in Ukrainian.
/// Call CheckAndRequestAllAsync() before starting any audio/BT operations.
/// </summary>
public class PermissionService
{
    public record PermissionResult(bool AllGranted, IReadOnlyList<string> Denied);

    public static async Task<PermissionResult> CheckAndRequestAllAsync()
    {
        var denied = new List<string>();

        // Microphone
        var mic = await Permissions.CheckStatusAsync<Permissions.Microphone>();
        if (mic != PermissionStatus.Granted)
        {
            mic = await Permissions.RequestAsync<Permissions.Microphone>();
            if (mic != PermissionStatus.Granted)
                denied.Add("Мікрофон");
        }

        // Bluetooth (Android API 31+ only, iOS handled by Info.plist)
        var bt = await Permissions.CheckStatusAsync<Permissions.Bluetooth>();
        if (bt != PermissionStatus.Granted)
        {
            bt = await Permissions.RequestAsync<Permissions.Bluetooth>();
            if (bt != PermissionStatus.Granted)
                denied.Add("Bluetooth");
        }

        // Storage (for music file picker)
        var storage = await Permissions.CheckStatusAsync<Permissions.StorageRead>();
        if (storage != PermissionStatus.Granted)
        {
            storage = await Permissions.RequestAsync<Permissions.StorageRead>();
            if (storage != PermissionStatus.Granted)
                denied.Add("Сховище (для файлів музики)");
        }

        return new PermissionResult(denied.Count == 0, denied);
    }

    public static async Task<bool> EnsureMicrophoneAsync()
    {
        var status = await Permissions.CheckStatusAsync<Permissions.Microphone>();
        if (status == PermissionStatus.Granted) return true;
        status = await Permissions.RequestAsync<Permissions.Microphone>();
        return status == PermissionStatus.Granted;
    }

    public static async Task<bool> EnsureBluetoothAsync()
    {
        var status = await Permissions.CheckStatusAsync<Permissions.Bluetooth>();
        if (status == PermissionStatus.Granted) return true;
        status = await Permissions.RequestAsync<Permissions.Bluetooth>();
        return status == PermissionStatus.Granted;
    }
}
