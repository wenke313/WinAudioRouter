using System.Management;
using Microsoft.Extensions.Logging;
using NAudio.CoreAudioApi;
using WinAudioRouter.Core.Audio.Events;
using WinAudioRouter.Core.Audio.Models;
using WinAudioRouter.Native.Wrappers;

namespace WinAudioRouter.Core.Audio.Services;

public class AudioDeviceManager : IAudioDeviceManager, IDisposable
{
    private readonly ILogger<AudioDeviceManager> _logger;
    private readonly MMDeviceEnumerator _enumerator;
    private readonly ManagementEventWatcher? _arrivalWatcher;
    private readonly ManagementEventWatcher? _removalWatcher;
    private readonly Timer _defaultDeviceTimer;
    private string? _lastDefaultPlaybackId;
    private string? _lastDefaultRecordingId;
    private IReadOnlyList<AudioDevice> _cachedPlaybackDevices = [];
    private bool _disposed;
    private DateTime _lastSwitchAttempt = DateTime.MinValue;

    public event EventHandler<DeviceChangedEventArgs>? DeviceChanged;
    public event EventHandler<DefaultDeviceChangedEventArgs>? DefaultDeviceChanged;

    public AudioDeviceManager(ILogger<AudioDeviceManager> logger)
    {
        _logger = logger;
        _enumerator = new MMDeviceEnumerator();

        _lastDefaultPlaybackId = GetDefaultDeviceId(DataFlow.Render);
        _lastDefaultRecordingId = GetDefaultDeviceId(DataFlow.Capture);

        try
        {
            var arrivalQuery = new WqlEventQuery(
                "SELECT * FROM __InstanceCreationEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_SoundDevice'");
            _arrivalWatcher = new ManagementEventWatcher(arrivalQuery);
            _arrivalWatcher.EventArrived += OnDeviceArrived;
            _arrivalWatcher.Start();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start device arrival watcher");
            _arrivalWatcher = null;
        }

        try
        {
            var removalQuery = new WqlEventQuery(
                "SELECT * FROM __InstanceDeletionEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_SoundDevice'");
            _removalWatcher = new ManagementEventWatcher(removalQuery);
            _removalWatcher.EventArrived += OnDeviceRemoved;
            _removalWatcher.Start();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start device removal watcher");
            _removalWatcher = null;
        }

        _defaultDeviceTimer = new Timer(
            CheckDefaultDeviceChange, null,
            TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }

    public Task<IReadOnlyList<AudioDevice>> GetPlaybackDevicesAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            try
            {
                _logger.LogInformation("Enumerating audio playback devices...");

                var defaultId = GetDefaultDeviceId(DataFlow.Render);
                var devices = new List<AudioDevice>();

                foreach (var mmDevice in _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
                {
                    try
                    {
                        var device = CreateAudioDevice(mmDevice, mmDevice.ID == defaultId, isPlayback: true);
                        devices.Add(device);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to read properties of playback device {DeviceId}", mmDevice.ID);
                    }
                    finally
                    {
                        mmDevice.Dispose();
                    }
                }

                _cachedPlaybackDevices = devices;
                _logger.LogInformation("Found {Count} playback audio devices", devices.Count);
                return (IReadOnlyList<AudioDevice>)devices;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enumerate playback devices");
                return (IReadOnlyList<AudioDevice>)new List<AudioDevice>();
            }
        }, cancellationToken);
    }

    public Task<IReadOnlyList<AudioDevice>> GetRecordingDevicesAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            try
            {
                _logger.LogInformation("Enumerating audio recording devices...");

                var defaultId = GetDefaultDeviceId(DataFlow.Capture);
                var devices = new List<AudioDevice>();

                foreach (var mmDevice in _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
                {
                    try
                    {
                        var device = CreateAudioDevice(mmDevice, mmDevice.ID == defaultId, isPlayback: false);
                        devices.Add(device);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to read properties of recording device {DeviceId}", mmDevice.ID);
                    }
                    finally
                    {
                        mmDevice.Dispose();
                    }
                }

                _logger.LogInformation("Found {Count} recording audio devices", devices.Count);
                return (IReadOnlyList<AudioDevice>)devices;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enumerate recording devices");
                return (IReadOnlyList<AudioDevice>)new List<AudioDevice>();
            }
        }, cancellationToken);
    }

    public Task<AudioDevice?> GetDefaultPlaybackDeviceAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var mmDevice = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            if (mmDevice is null)
            {
                _logger.LogWarning("No default playback device found");
                return Task.FromResult<AudioDevice?>(null);
            }

            var device = CreateAudioDevice(mmDevice, isDefault: true, isPlayback: true);
            return Task.FromResult<AudioDevice?>(device);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get default playback device");
            return Task.FromResult<AudioDevice?>(null);
        }
    }

    public Task<AudioDevice?> GetDefaultRecordingDeviceAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var mmDevice = _enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
            if (mmDevice is null)
            {
                _logger.LogWarning("No default recording device found");
                return Task.FromResult<AudioDevice?>(null);
            }

            var device = CreateAudioDevice(mmDevice, isDefault: true, isPlayback: false);
            return Task.FromResult<AudioDevice?>(device);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get default recording device");
            return Task.FromResult<AudioDevice?>(null);
        }
    }

    public async Task<bool> SetDefaultPlaybackDeviceAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        if ((DateTime.UtcNow - _lastSwitchAttempt).TotalSeconds < 1)
        {
            _logger.LogWarning("Switch default device called too quickly, throttling");
            return false;
        }
        _lastSwitchAttempt = DateTime.UtcNow;

        _logger.LogInformation("Setting default playback device to {DeviceId}", deviceId);

        var oldDeviceId = GetDefaultDeviceId(DataFlow.Render);

        var result = DefaultDeviceSwitcher.SwitchDefaultDevice(deviceId, AudioDataFlow.Render);

        if (!result.Success)
        {
            _logger.LogError("Failed to set default playback device to {DeviceId}: {Error}", deviceId, result.Error);
            return false;
        }

        await Task.Delay(500, cancellationToken).ConfigureAwait(false);

        var verifiedDeviceId = GetDefaultDeviceId(DataFlow.Render);
        if (verifiedDeviceId != deviceId)
        {
            _logger.LogWarning("Default playback device verification failed: expected {Expected}, got {Actual}", deviceId, verifiedDeviceId);
            return false;
        }

        _lastDefaultPlaybackId = verifiedDeviceId;

        try
        {
            _cachedPlaybackDevices = await GetPlaybackDevicesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh cached playback devices after switch");
        }

        _logger.LogInformation("Successfully set default playback device to {DeviceId}", deviceId);
        OnDefaultDeviceChanged(oldDeviceId, verifiedDeviceId);

        return true;
    }

    public async Task<bool> SetDefaultRecordingDeviceAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Setting default recording device to {DeviceId}", deviceId);

        var oldDeviceId = GetDefaultDeviceId(DataFlow.Capture);

        var result = DefaultDeviceSwitcher.SwitchDefaultDevice(deviceId, AudioDataFlow.Capture);

        if (!result.Success)
        {
            _logger.LogError("Failed to set default recording device to {DeviceId}: {Error}", deviceId, result.Error);
            return false;
        }

        await Task.Delay(500, cancellationToken).ConfigureAwait(false);

        var verifiedDeviceId = GetDefaultDeviceId(DataFlow.Capture);
        if (verifiedDeviceId != deviceId)
        {
            _logger.LogWarning("Default recording device verification failed: expected {Expected}, got {Actual}", deviceId, verifiedDeviceId);
            return false;
        }

        _lastDefaultRecordingId = verifiedDeviceId;

        _logger.LogInformation("Successfully set default recording device to {DeviceId}", deviceId);
        OnDefaultDeviceChanged(oldDeviceId, verifiedDeviceId);

        return true;
    }

    public Task<bool> SetDeviceVolumeAsync(string deviceId, int volumeLevel, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            try
            {
                if (volumeLevel < 0) volumeLevel = 0;
                if (volumeLevel > 100) volumeLevel = 100;

                using var mmDevice = _enumerator.GetDevice(deviceId);
                if (mmDevice is null)
                {
                    _logger.LogWarning("Device {DeviceId} not found for volume set", deviceId);
                    return false;
                }

                mmDevice.AudioEndpointVolume.MasterVolumeLevelScalar = volumeLevel / 100f;
                _logger.LogInformation("Set device {DeviceId} volume to {Volume}%", deviceId, volumeLevel);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set volume for device {DeviceId}", deviceId);
                return false;
            }
        }, cancellationToken);
    }

    private string? GetDefaultDeviceId(DataFlow dataFlow)
    {
        try
        {
            using var defaultDevice = _enumerator.GetDefaultAudioEndpoint(dataFlow, Role.Multimedia);
            return defaultDevice?.ID;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get default device ID for {DataFlow}", dataFlow);
            return null;
        }
    }

    private void OnDefaultDeviceChanged(string? oldDeviceId, string? newDeviceId)
    {
        DefaultDeviceChanged?.Invoke(this, new DefaultDeviceChangedEventArgs
        {
            OldDeviceId = oldDeviceId,
            NewDeviceId = newDeviceId
        });
    }

    private void OnDeviceArrived(object sender, EventArrivedEventArgs e)
    {
        try
        {
            var targetInstance = (ManagementBaseObject?)e.NewEvent?["TargetInstance"];
            var deviceId = (targetInstance as ManagementBaseObject)?["DeviceID"]?.ToString();
            var deviceName = (targetInstance as ManagementBaseObject)?["Name"]?.ToString();

            _logger.LogInformation("Audio device added: {DeviceName} ({DeviceId})", deviceName, deviceId);

            DeviceChanged?.Invoke(this, new DeviceChangedEventArgs
            {
                DeviceId = deviceId,
                DeviceName = deviceName,
                IsAdded = true,
                IsRemoved = false
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error processing device arrival event");
        }
    }

    private void OnDeviceRemoved(object sender, EventArrivedEventArgs e)
    {
        try
        {
            var targetInstance = (ManagementBaseObject?)e.NewEvent?["TargetInstance"];
            var deviceId = (targetInstance as ManagementBaseObject)?["DeviceID"]?.ToString();
            var deviceName = (targetInstance as ManagementBaseObject)?["Name"]?.ToString();

            _logger.LogInformation("Audio device removed: {DeviceName} ({DeviceId})", deviceName, deviceId);

            DeviceChanged?.Invoke(this, new DeviceChangedEventArgs
            {
                DeviceId = deviceId,
                DeviceName = deviceName,
                IsAdded = false,
                IsRemoved = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error processing device removal event");
        }
    }

    private void CheckDefaultDeviceChange(object? state)
    {
        try
        {
            var currentPlaybackId = GetDefaultDeviceId(DataFlow.Render);
            if (currentPlaybackId != _lastDefaultPlaybackId &&
                currentPlaybackId is not null && _lastDefaultPlaybackId is not null)
            {
                var oldId = _lastDefaultPlaybackId;
                _lastDefaultPlaybackId = currentPlaybackId;

                _logger.LogInformation("Default playback device changed from {OldId} to {NewId}", oldId, currentPlaybackId);

                DefaultDeviceChanged?.Invoke(this, new DefaultDeviceChangedEventArgs
                {
                    OldDeviceId = oldId,
                    NewDeviceId = currentPlaybackId
                });
            }

            var currentRecordingId = GetDefaultDeviceId(DataFlow.Capture);
            if (currentRecordingId != _lastDefaultRecordingId &&
                currentRecordingId is not null && _lastDefaultRecordingId is not null)
            {
                var oldId = _lastDefaultRecordingId;
                _lastDefaultRecordingId = currentRecordingId;

                _logger.LogInformation("Default recording device changed from {OldId} to {NewId}", oldId, currentRecordingId);

                DefaultDeviceChanged?.Invoke(this, new DefaultDeviceChangedEventArgs
                {
                    OldDeviceId = oldId,
                    NewDeviceId = currentRecordingId
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking default device change");
        }
    }

    private AudioDevice CreateAudioDevice(MMDevice mmDevice, bool isDefault, bool isPlayback)
    {
        int volumeLevel = 0;
        int samplingRate = 0;
        int bitDepth = 0;
        double defaultPeriodMs = 0;
        double minimumPeriodMs = 0;

        try
        {
            volumeLevel = (int)(mmDevice.AudioEndpointVolume.MasterVolumeLevelScalar * 100);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get volume for device {DeviceId}", mmDevice.ID);
        }

        try
        {
            var audioClient = mmDevice.AudioClient;
            var mixFormat = audioClient.MixFormat;
            samplingRate = mixFormat.SampleRate;
            bitDepth = mixFormat.BitsPerSample;

            defaultPeriodMs = audioClient.DefaultDevicePeriod / 10000.0;
            minimumPeriodMs = audioClient.MinimumDevicePeriod / 10000.0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get mix format for device {DeviceId}", mmDevice.ID);
        }

        var name = mmDevice.FriendlyName;
        var deviceType = DetectDeviceType(name, isPlayback);

        return new AudioDevice
        {
            Id = mmDevice.ID,
            Name = name,
            Description = name,
            DeviceType = deviceType,
            IsActive = mmDevice.State == DeviceState.Active,
            IsDefault = isDefault,
            IsEnabled = mmDevice.State != DeviceState.Disabled,
            VolumeLevel = volumeLevel,
            SamplingRate = samplingRate,
            BitDepth = bitDepth,
            DefaultPeriodMs = Math.Round(defaultPeriodMs, 1),
            MinimumPeriodMs = Math.Round(minimumPeriodMs, 1)
        };
    }

    private static AudioDeviceType DetectDeviceType(string name, bool isPlayback)
    {
        var lowerName = name.ToLowerInvariant();

        if (lowerName.Contains("bluetooth"))
            return AudioDeviceType.Bluetooth;
        if (lowerName.Contains("usb"))
            return AudioDeviceType.Usb;
        if (lowerName.Contains("headphone") || lowerName.Contains("headset") || lowerName.Contains("earbuds") || lowerName.Contains("耳机"))
            return AudioDeviceType.Headphone;
        if (lowerName.Contains("speaker") || lowerName.Contains("扬声器"))
            return AudioDeviceType.Speaker;
        if (lowerName.Contains("microphone") || lowerName.Contains("麦克风"))
            return AudioDeviceType.Microphone;

        return isPlayback ? AudioDeviceType.Speaker : AudioDeviceType.Microphone;
    }

    public void Dispose()
    {
        if (_disposed) return;

        try { _arrivalWatcher?.Stop(); } catch { }
        try { _arrivalWatcher?.Dispose(); } catch { }

        try { _removalWatcher?.Stop(); } catch { }
        try { _removalWatcher?.Dispose(); } catch { }

        _defaultDeviceTimer?.Dispose();
        _enumerator?.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
