using System.Text.Json;
using Microsoft.Extensions.Logging;
using WinAudioRouter.Core.Bluetooth.Models;

namespace WinAudioRouter.Core.Bluetooth.Services;

public class BluetoothSettingsService : IBluetoothSettingsService
{
    private readonly ILogger<BluetoothSettingsService> _logger;
    private readonly IBluetoothService _bluetoothService;
    private readonly string _configDirectory;
    private readonly string _configFilePath;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private List<BluetoothAudioDevice> _cachedDevices = [];
    private bool _isAutoReconnectEnabled = true;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public bool IsAutoReconnectEnabled
    {
        get => _isAutoReconnectEnabled;
        set => _isAutoReconnectEnabled = value;
    }

    public BluetoothSettingsService(
        ILogger<BluetoothSettingsService> logger,
        IBluetoothService bluetoothService)
    {
        _logger = logger;
        _bluetoothService = bluetoothService;
        _configDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WinAudioRouter");
        _configFilePath = Path.Combine(_configDirectory, "bluetooth_devices.json");
    }

    public async Task SaveDeviceAsync(BluetoothAudioDevice device)
    {
        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            await LoadCacheIfNeededAsync().ConfigureAwait(false);

            var existingIndex = _cachedDevices.FindIndex(d => d.Id == device.Id);
            if (existingIndex >= 0)
            {
                _cachedDevices[existingIndex] = device;
            }
            else
            {
                _cachedDevices.Add(device);
            }

            await PersistAsync().ConfigureAwait(false);
            _logger.LogInformation("Saved Bluetooth device {DeviceName} ({DeviceId})", device.Name, device.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save Bluetooth device {DeviceId}", device.Id);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task RemoveDeviceAsync(string deviceId)
    {
        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            await LoadCacheIfNeededAsync().ConfigureAwait(false);

            var removed = _cachedDevices.RemoveAll(d => d.Id == deviceId);
            if (removed > 0)
            {
                await PersistAsync().ConfigureAwait(false);
                _logger.LogInformation("Removed Bluetooth device {DeviceId}", deviceId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove Bluetooth device {DeviceId}", deviceId);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<IReadOnlyList<BluetoothAudioDevice>> GetSavedDevicesAsync()
    {
        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            await LoadCacheIfNeededAsync().ConfigureAwait(false);
            return _cachedDevices.AsReadOnly();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<bool> AutoReconnectAsync(CancellationToken ct = default)
    {
        if (!_isAutoReconnectEnabled)
        {
            _logger.LogInformation("Auto-reconnect is disabled");
            return false;
        }

        try
        {
            var savedDevices = await GetSavedDevicesAsync().ConfigureAwait(false);
            if (savedDevices.Count == 0)
            {
                _logger.LogInformation("No saved Bluetooth devices to auto-reconnect");
                return false;
            }

            var discoveredDevices = await _bluetoothService.DiscoverDevicesAsync(ct).ConfigureAwait(false);
            var reconnectedAny = false;

            foreach (var savedDevice in savedDevices)
            {
                var discovered = discoveredDevices.FirstOrDefault(d => d.BluetoothAddress == savedDevice.BluetoothAddress);
                if (discovered == null) continue;

                if (discovered.IsPaired && !discovered.IsConnected)
                {
                    var connected = await _bluetoothService.ConnectAsync(discovered.BluetoothAddress, ct).ConfigureAwait(false);
                    if (connected)
                    {
                        reconnectedAny = true;
                        _logger.LogInformation("Auto-reconnected to {DeviceName}", discovered.Name);
                    }
                }
                else if (discovered.IsConnected)
                {
                    reconnectedAny = true;
                    _logger.LogInformation("Device {DeviceName} is already connected", discovered.Name);
                }
            }

            return reconnectedAny;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Auto-reconnect was cancelled");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to auto-reconnect Bluetooth devices");
            return false;
        }
    }

    private async Task LoadCacheIfNeededAsync()
    {
        if (_cachedDevices.Count > 0 && File.Exists(_configFilePath)) return;

        if (!File.Exists(_configFilePath))
        {
            _cachedDevices = [];
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_configFilePath).ConfigureAwait(false);
            var devices = JsonSerializer.Deserialize<List<BluetoothAudioDevice>>(json, JsonOptions);
            _cachedDevices = devices ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load Bluetooth devices from config, starting with empty list");
            _cachedDevices = [];
        }
    }

    private async Task PersistAsync()
    {
        try
        {
            Directory.CreateDirectory(_configDirectory);
            var json = JsonSerializer.Serialize(_cachedDevices, JsonOptions);
            await File.WriteAllTextAsync(_configFilePath, json).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist Bluetooth devices config");
            throw;
        }
    }
}
