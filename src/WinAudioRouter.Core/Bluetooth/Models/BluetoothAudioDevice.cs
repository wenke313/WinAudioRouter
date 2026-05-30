namespace WinAudioRouter.Core.Bluetooth.Models;

public class BluetoothAudioDevice
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsConnected { get; set; }
    public bool IsPaired { get; set; }
    public BluetoothDeviceType DeviceType { get; set; }
    public ulong BluetoothAddress { get; set; }
    public DateTime? LastSeenAt { get; set; }
}
