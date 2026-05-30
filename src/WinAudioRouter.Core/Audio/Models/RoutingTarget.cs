using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WinAudioRouter.Core.Audio.Models;

public class RoutingTarget : INotifyPropertyChanged
{
    public string DeviceId { get; }
    public string DeviceName { get; }
    public double DefaultPeriodMs { get; }
    public double MinimumPeriodMs { get; }

    private int _latencyMs;
    public int LatencyMs
    {
        get => _latencyMs;
        set
        {
            _latencyMs = Math.Clamp(value, 0, 2000);
            OnPropertyChanged();
            OnPropertyChanged(nameof(LatencyLabel));
        }
    }

    private int _volumeLevel;
    public int VolumeLevel
    {
        get => _volumeLevel;
        set { _volumeLevel = value; OnPropertyChanged(); }
    }

    public string LatencyLabel => LatencyMs == 0 ? "默认" : $"{LatencyMs}ms";

    public string DeviceLatencyInfo => MinimumPeriodMs > 0
        ? $"设备延迟: 默认 {DefaultPeriodMs:F1}ms / 最低 {MinimumPeriodMs:F1}ms"
        : $"设备延迟: 默认 {DefaultPeriodMs:F1}ms";

    public int PendingLatencyMs { get; set; }

    public RoutingTarget(string deviceId, string deviceName, int volumeLevel = 100, int latencyMs = 0,
        double defaultPeriodMs = 0, double minimumPeriodMs = 0)
    {
        DeviceId = deviceId;
        DeviceName = deviceName;
        _volumeLevel = volumeLevel;
        _latencyMs = Math.Clamp(latencyMs, 0, 2000);
        PendingLatencyMs = _latencyMs;
        DefaultPeriodMs = defaultPeriodMs;
        MinimumPeriodMs = minimumPeriodMs;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
