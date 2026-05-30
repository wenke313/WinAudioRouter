namespace WinAudioRouter.Core.Audio.Models;

public class AudioDevice
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public AudioDeviceType DeviceType { get; set; }
    public bool IsActive { get; set; }
    public bool IsDefault { get; set; }
    public bool IsEnabled { get; set; }
    public int VolumeLevel { get; set; }
    public int SamplingRate { get; set; }
    public int BitDepth { get; set; }
    public double DefaultPeriodMs { get; set; }
    public double MinimumPeriodMs { get; set; }
}
