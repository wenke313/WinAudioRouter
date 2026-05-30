using NAudio.CoreAudioApi;
using NAudio.Wave;
using WinAudioRouter.Core.Audio.Events;

namespace WinAudioRouter.Core.Audio.Services;

public interface IAudioCaptureService
{
    bool IsCapturing { get; }

    event EventHandler<AudioCapturedEventArgs>? AudioCaptured;

    MMDevice GetDeviceById(string deviceId);
    WaveFormat GetDeviceWaveFormat(string deviceId);

    Task StartCaptureAsync(string? deviceId = null, CancellationToken cancellationToken = default);
    Task StopCaptureAsync(CancellationToken cancellationToken = default);
}
