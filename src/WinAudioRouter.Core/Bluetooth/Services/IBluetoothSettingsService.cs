using WinAudioRouter.Core.Bluetooth.Models;

namespace WinAudioRouter.Core.Bluetooth.Services;

public interface IBluetoothSettingsService
{
    Task SaveDeviceAsync(BluetoothAudioDevice device);
    Task RemoveDeviceAsync(string deviceId);
    Task<IReadOnlyList<BluetoothAudioDevice>> GetSavedDevicesAsync();
    Task<bool> AutoReconnectAsync(CancellationToken ct = default);
    bool IsAutoReconnectEnabled { get; set; }
}
