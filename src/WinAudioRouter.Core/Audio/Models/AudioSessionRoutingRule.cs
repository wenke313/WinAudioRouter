namespace WinAudioRouter.Core.Audio.Models;

public class AudioSessionRoutingRule
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ProcessName { get; set; } = string.Empty;
    public string TargetDeviceId { get; set; } = string.Empty;
    public string TargetDeviceName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
