namespace WinAudioRouter.Core.Audio.Models;

public class AudioSession
{
    public string Id { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;
    public int ProcessId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string IconPath { get; set; } = string.Empty;
    public float Volume { get; set; }
    public bool IsMuted { get; set; }
    public string? CurrentDeviceId { get; set; }
}
