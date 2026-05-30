namespace WinAudioRouter.Core.Configuration;

public class BluetoothConfiguration
{
    public bool AutoReconnectEnabled { get; set; } = true;
    public int ScanTimeoutSeconds { get; set; } = 10;
    public int ReconnectIntervalSeconds { get; set; } = 30;
    public List<string> SavedDeviceIds { get; set; } = [];
}
