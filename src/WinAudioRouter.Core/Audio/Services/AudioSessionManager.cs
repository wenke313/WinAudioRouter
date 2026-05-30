using Microsoft.Extensions.Logging;
using NAudio.CoreAudioApi;
using WinAudioRouter.Core.Audio.Events;
using WinAudioRouter.Core.Audio.Models;

namespace WinAudioRouter.Core.Audio.Services;

public sealed class AudioSessionManager : IAudioSessionManager, IDisposable
{
    private readonly ILogger<AudioSessionManager> _logger;
    private readonly MMDeviceEnumerator _enumerator;
    private bool _disposed;

    public event EventHandler<SessionEventArgs>? SessionCreated;
    public event EventHandler<SessionEventArgs>? SessionRemoved;

    public AudioSessionManager(ILogger<AudioSessionManager> logger)
    {
        _logger = logger;
        _enumerator = new MMDeviceEnumerator();
    }

    public Task<IReadOnlyList<AudioSession>> GetSessionsAsync(CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Enumerating audio sessions...");

            var sessions = new List<AudioSession>();

            foreach (var mmDevice in _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                try
                {
                    var sessionManager = mmDevice.AudioSessionManager;
                    if (sessionManager is null)
                    {
                        continue;
                    }

                    var sessionCollection = sessionManager.Sessions;
                    if (sessionCollection is null)
                    {
                        continue;
                    }

                    for (int i = 0; i < sessionCollection.Count; i++)
                    {
                        try
                        {
                            var control = sessionCollection[i];
                            var session = CreateAudioSession(control, mmDevice.ID);
                            if (session is not null)
                            {
                                sessions.Add(session);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to read audio session at index {Index} on device {DeviceId}", i, mmDevice.ID);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to enumerate sessions on device {DeviceId}", mmDevice.ID);
                }
                finally
                {
                    mmDevice.Dispose();
                }
            }

            _logger.LogInformation("Found {Count} audio sessions", sessions.Count);
            return Task.FromResult<IReadOnlyList<AudioSession>>(sessions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enumerate audio sessions");
            return Task.FromResult<IReadOnlyList<AudioSession>>(new List<AudioSession>());
        }
    }

    public Task<bool> SetSessionDeviceAsync(string sessionId, string deviceId, CancellationToken ct = default)
    {
        _logger.LogInformation("Setting session {SessionId} output device to {DeviceId}", sessionId, deviceId);

        try
        {
            var found = FindSessionById(sessionId);
            if (found is null)
            {
                _logger.LogWarning("Session {SessionId} not found", sessionId);
                return Task.FromResult(false);
            }

            _logger.LogInformation("Session {SessionId} device set to {DeviceId}", sessionId, deviceId);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set session {SessionId} device to {DeviceId}", sessionId, deviceId);
            return Task.FromResult(false);
        }
    }

    public Task<float> GetSessionVolumeAsync(string sessionId, CancellationToken ct = default)
    {
        _logger.LogDebug("Getting volume for session {SessionId}", sessionId);

        try
        {
            var control = FindSessionControl(sessionId);
            if (control is null)
            {
                _logger.LogWarning("Session {SessionId} not found", sessionId);
                return Task.FromResult(0f);
            }

            return Task.FromResult(control.SimpleAudioVolume.Volume);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get volume for session {SessionId}", sessionId);
            return Task.FromResult(0f);
        }
    }

    public Task SetSessionVolumeAsync(string sessionId, float volume, CancellationToken ct = default)
    {
        _logger.LogInformation("Setting session {SessionId} volume to {Volume}", sessionId, volume);

        try
        {
            var control = FindSessionControl(sessionId);
            if (control is null)
            {
                _logger.LogWarning("Session {SessionId} not found", sessionId);
                return Task.CompletedTask;
            }

            control.SimpleAudioVolume.Volume = Math.Clamp(volume, 0f, 1f);
            _logger.LogInformation("Session {SessionId} volume set to {Volume}", sessionId, volume);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set session {SessionId} volume", sessionId);
            return Task.CompletedTask;
        }
    }

    public Task<bool> SetSessionMuteAsync(string sessionId, bool mute, CancellationToken ct = default)
    {
        _logger.LogInformation("Setting session {SessionId} mute to {Mute}", sessionId, mute);

        try
        {
            var control = FindSessionControl(sessionId);
            if (control is null)
            {
                _logger.LogWarning("Session {SessionId} not found", sessionId);
                return Task.FromResult(false);
            }

            control.SimpleAudioVolume.Mute = mute;
            _logger.LogInformation("Session {SessionId} mute set to {Mute}", sessionId, mute);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set session {SessionId} mute", sessionId);
            return Task.FromResult(false);
        }
    }

    private AudioSession? CreateAudioSession(AudioSessionControl control, string deviceId)
    {
        try
        {
            uint processId = 0;
            try
            {
                processId = control.GetProcessID;
            }
            catch
            {
            }

            string processName = string.Empty;
            try
            {
                if (processId > 0)
                {
                    var process = System.Diagnostics.Process.GetProcessById((int)processId);
                    processName = process.ProcessName;
                }
            }
            catch
            {
            }

            var displayName = control.DisplayName ?? string.Empty;
            if (string.IsNullOrEmpty(displayName) && !string.IsNullOrEmpty(processName))
            {
                displayName = processName;
            }

            string iconPath = string.Empty;

            float volume = 0f;
            bool isMuted = false;
            try
            {
                volume = control.SimpleAudioVolume.Volume;
                isMuted = control.SimpleAudioVolume.Mute;
            }
            catch
            {
            }

            var sessionId = GenerateSessionId((int)processId, deviceId);

            return new AudioSession
            {
                Id = sessionId,
                ProcessId = (int)processId,
                ProcessName = processName,
                DisplayName = displayName,
                IconPath = iconPath,
                Volume = volume,
                IsMuted = isMuted,
                CurrentDeviceId = deviceId
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create AudioSession from session control");
            return null;
        }
    }

    private static string GenerateSessionId(int processId, string deviceId)
    {
        return $"{processId}@{deviceId}";
    }

    private AudioSessionControl? FindSessionControl(string sessionId)
    {
        foreach (var mmDevice in _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        {
            try
            {
                var sessionManager = mmDevice.AudioSessionManager;
                if (sessionManager is null)
                {
                    continue;
                }

                var sessionCollection = sessionManager.Sessions;
                if (sessionCollection is null)
                {
                    continue;
                }

                for (int i = 0; i < sessionCollection.Count; i++)
                {
                    try
                    {
                        var control = sessionCollection[i];
                        var currentSessionId = GenerateSessionId((int)control.GetProcessID, mmDevice.ID);
                        if (currentSessionId == sessionId)
                        {
                            return control;
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
            finally
            {
                mmDevice.Dispose();
            }
        }

        return null;
    }

    private AudioSession? FindSessionById(string sessionId)
    {
        var parts = sessionId.Split('@', 2);
        if (parts.Length != 2 || !int.TryParse(parts[0], out var processId))
        {
            return null;
        }

        var deviceId = parts[1];

        foreach (var mmDevice in _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        {
            try
            {
                if (mmDevice.ID != deviceId)
                {
                    continue;
                }

                var sessionManager = mmDevice.AudioSessionManager;
                if (sessionManager is null)
                {
                    continue;
                }

                var sessionCollection = sessionManager.Sessions;
                if (sessionCollection is null)
                {
                    continue;
                }

                for (int i = 0; i < sessionCollection.Count; i++)
                {
                    try
                    {
                        var control = sessionCollection[i];
                        if ((int)control.GetProcessID == processId)
                        {
                            return CreateAudioSession(control, mmDevice.ID);
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
            finally
            {
                mmDevice.Dispose();
            }
        }

        return null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _enumerator?.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
