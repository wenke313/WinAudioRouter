namespace WinAudioRouter.Core.Audio.Models;

public class CaptureStatistics
{
    public long TotalBytesCaptured { get; set; }
    public int CaptureCount { get; set; }
    public DateTime? StartTime { get; set; }
    public double AverageBytesPerSecond { get; set; }
}
