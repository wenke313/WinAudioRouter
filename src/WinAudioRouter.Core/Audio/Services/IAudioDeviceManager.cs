using WinAudioRouter.Core.Audio.Models;

namespace WinAudioRouter.Core.Audio.Services;

public interface IAudioDeviceManager
{
    Task<IReadOnlyList<AudioDevice>> GetPlaybackDevicesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AudioDevice>> GetRecordingDevicesAsync(CancellationToken cancellationToken = default);
    Task<AudioDevice?> GetDefaultPlaybackDeviceAsync(CancellationToken cancellationToken = default);
    Task<AudioDevice?> GetDefaultRecordingDeviceAsync(CancellationToken cancellationToken = default);
    Task<bool> SetDefaultPlaybackDeviceAsync(string deviceId, CancellationToken cancellationToken = default);
    Task<bool> SetDefaultRecordingDeviceAsync(string deviceId, CancellationToken cancellationToken = default);
    Task<bool> SetDeviceVolumeAsync(string deviceId, int volumeLevel, CancellationToken cancellationToken = default);

    event EventHandler<Events.DeviceChangedEventArgs>? DeviceChanged;
    event EventHandler<Events.DefaultDeviceChangedEventArgs>? DefaultDeviceChanged;
}
