namespace WinAudioRouter.Core.Audio.Models;

public class AppRoutingRule
{
    public string RuleId { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;
    public string TargetDeviceId { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
}
