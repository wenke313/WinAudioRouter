using WinAudioRouter.Core.Audio.Events;
using WinAudioRouter.Core.Audio.Models;

namespace WinAudioRouter.Core.Audio.Services;

public interface IAudioRouterEngine
{
    bool IsRouting { get; }
    string? SourceDeviceId { get; }
    IReadOnlyList<RoutingTarget> ActiveTargets { get; }
    int CaptureLatencyMs { get; set; }

    event EventHandler<RoutingStateChangedEventArgs>? RoutingStateChanged;

    Task StartRoutingAsync(string sourceDeviceId, IReadOnlyList<RoutingTarget> targets, int captureLatencyMs = 0, CancellationToken cancellationToken = default);
    Task StopRoutingAsync(CancellationToken cancellationToken = default);
    Task UpdateTargetLatencyAsync(string targetDeviceId, int latencyMs);
    Task UpdateCaptureLatencyAsync(int latencyMs);
    bool UpdateTargetVolume(string targetDeviceId, float volume);
}
