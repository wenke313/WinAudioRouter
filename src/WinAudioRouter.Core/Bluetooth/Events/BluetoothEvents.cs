using WinAudioRouter.Core.Bluetooth.Models;

namespace WinAudioRouter.Core.Bluetooth.Events;

public class BluetoothDeviceEventArgs : EventArgs
{
    public required BluetoothAudioDevice Device { get; init; }
}
