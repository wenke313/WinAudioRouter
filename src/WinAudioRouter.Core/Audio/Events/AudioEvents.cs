using WinAudioRouter.Core.Audio.Models;

namespace WinAudioRouter.Core.Audio.Events;

public class DeviceChangedEventArgs : EventArgs
{
    public string? DeviceId { get; set; }
    public string? DeviceName { get; set; }
    public bool IsAdded { get; set; }
    public bool IsRemoved { get; set; }
}

public class DefaultDeviceChangedEventArgs : EventArgs
{
    public string? OldDeviceId { get; set; }
    public string? NewDeviceId { get; set; }
    public string? NewDeviceName { get; set; }
}

public class AudioCapturedEventArgs : EventArgs
{
    public byte[]? AudioData { get; set; }
    public int Length { get; set; }
    public int SamplingRate { get; set; }
    public int BitDepth { get; set; }
}

public class RoutingStateChangedEventArgs : EventArgs
{
    public bool IsRouting { get; init; }
    public string? SourceDeviceId { get; init; }
}

public class SessionEventArgs : EventArgs
{
    public required AudioSession Session { get; init; }
}
