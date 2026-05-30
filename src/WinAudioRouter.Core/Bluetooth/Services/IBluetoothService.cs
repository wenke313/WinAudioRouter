using WinAudioRouter.Core.Bluetooth.Events;
using WinAudioRouter.Core.Bluetooth.Models;

namespace WinAudioRouter.Core.Bluetooth.Services;

public interface IBluetoothService
{
    Task<IReadOnlyList<BluetoothAudioDevice>> DiscoverDevicesAsync(CancellationToken ct = default);
    Task<bool> PairDeviceAsync(ulong address, CancellationToken ct = default);
    Task<bool> ConnectAsync(ulong address, CancellationToken ct = default);
    Task<bool> DisconnectAsync(ulong address, CancellationToken ct = default);
    Task StartMonitoringAsync(CancellationToken ct = default);
    Task StopMonitoringAsync();

    event EventHandler<BluetoothDeviceEventArgs>? DeviceDiscovered;
    event EventHandler<BluetoothDeviceEventArgs>? DeviceConnected;
    event EventHandler<BluetoothDeviceEventArgs>? DeviceDisconnected;
}
