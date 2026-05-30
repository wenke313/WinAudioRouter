using WinAudioRouter.Core.Audio.Events;
using WinAudioRouter.Core.Audio.Models;

namespace WinAudioRouter.Core.Audio.Services;

public interface IAudioSessionManager
{
    Task<IReadOnlyList<AudioSession>> GetSessionsAsync(CancellationToken ct = default);
    Task<bool> SetSessionDeviceAsync(string sessionId, string deviceId, CancellationToken ct = default);
    Task<float> GetSessionVolumeAsync(string sessionId, CancellationToken ct = default);
    Task SetSessionVolumeAsync(string sessionId, float volume, CancellationToken ct = default);
    Task<bool> SetSessionMuteAsync(string sessionId, bool mute, CancellationToken ct = default);

    event EventHandler<SessionEventArgs>? SessionCreated;
    event EventHandler<SessionEventArgs>? SessionRemoved;
}
