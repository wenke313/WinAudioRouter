using Microsoft.Extensions.Logging;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Enumeration;
using WinAudioRouter.Core.Bluetooth.Events;
using WinAudioRouter.Core.Bluetooth.Models;

namespace WinAudioRouter.Core.Bluetooth.Services;

public class BluetoothService : IBluetoothService, IDisposable
{
    private readonly ILogger<BluetoothService> _logger;
    private readonly Dictionary<ulong, BluetoothAudioDevice> _discoveredDevices = new();
    private readonly Dictionary<ulong, BluetoothDevice> _connectedDevices = new();
    private readonly Dictionary<ulong, DateTime> _monitoredDeviceLastSeen = new();
    private BluetoothLEAdvertisementWatcher? _watcher;
    private BluetoothLEAdvertisementWatcher? _monitorWatcher;
    private BluetoothAdapter? _adapter;
    private bool _disposed;
    private bool _isMonitoring;
    private PeriodicTimer? _monitorTimer;

    public event EventHandler<BluetoothDeviceEventArgs>? DeviceDiscovered;
    public event EventHandler<BluetoothDeviceEventArgs>? DeviceConnected;
    public event EventHandler<BluetoothDeviceEventArgs>? DeviceDisconnected;

    public BluetoothService(ILogger<BluetoothService> logger)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<BluetoothAudioDevice>> DiscoverDevicesAsync(CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Starting Bluetooth device discovery...");

            _adapter = await BluetoothAdapter.GetDefaultAsync().AsTask(ct).ConfigureAwait(false);
            if (_adapter is null)
            {
                _logger.LogWarning("No Bluetooth adapter found");
                return Array.Empty<BluetoothAudioDevice>();
            }

            _discoveredDevices.Clear();

            var pairedDevices = await GetPairedDevicesInternalAsync(ct).ConfigureAwait(false);
            foreach (var device in pairedDevices)
            {
                _discoveredDevices[device.BluetoothAddress] = device;
            }

            var tcs = new TaskCompletionSource<bool>();
            using var ctRegistration = ct.Register(() => tcs.TrySetCanceled());

            _watcher = new BluetoothLEAdvertisementWatcher
            {
                ScanningMode = BluetoothLEScanningMode.Active
            };

            _watcher.Received += OnAdvertisementReceived;
            _watcher.Stopped += (_, _) => tcs.TrySetResult(true);

            _watcher.Start();

            try
            {
                await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(10), ct)).ConfigureAwait(false);
            }
            finally
            {
                _watcher.Stop();
                _watcher.Received -= OnAdvertisementReceived;
            }

            _logger.LogInformation("Bluetooth discovery completed, found {Count} devices", _discoveredDevices.Count);
            return _discoveredDevices.Values.ToList().AsReadOnly();
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Bluetooth device discovery was cancelled");
            return _discoveredDevices.Values.ToList().AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to discover Bluetooth devices");
            return Array.Empty<BluetoothAudioDevice>();
        }
    }

    public async Task<bool> PairDeviceAsync(ulong address, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Pairing Bluetooth device {Address:X12}...", address);

            var btDevice = await BluetoothDevice.FromBluetoothAddressAsync(address).AsTask(ct).ConfigureAwait(false);
            if (btDevice is null)
            {
                _logger.LogWarning("Bluetooth device {Address:X12} not found", address);
                return false;
            }

            if (btDevice.DeviceInformation.Pairing.IsPaired)
            {
                _logger.LogInformation("Bluetooth device {Address:X12} is already paired", address);
                btDevice.Dispose();
                return true;
            }

            var pairingResult = await btDevice.DeviceInformation.Pairing.PairAsync(DevicePairingProtectionLevel.None).AsTask(ct).ConfigureAwait(false);
            var paired = pairingResult.Status == DevicePairingResultStatus.Paired;

            if (paired)
            {
                _logger.LogInformation("Successfully paired Bluetooth device {Address:X12}", address);
            }
            else
            {
                _logger.LogWarning("Failed to pair Bluetooth device {Address:X12}, status: {Status}", address, pairingResult.Status);
            }

            btDevice.Dispose();
            return paired;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Pairing Bluetooth device {Address:X12} was cancelled", address);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to pair Bluetooth device {Address:X12}", address);
            return false;
        }
    }

    public async Task<bool> ConnectAsync(ulong address, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Connecting to Bluetooth device {Address:X12}...", address);

            var btDevice = await BluetoothDevice.FromBluetoothAddressAsync(address).AsTask(ct).ConfigureAwait(false);
            if (btDevice is null)
            {
                _logger.LogWarning("Bluetooth device {Address:X12} not found", address);
                return false;
            }

            btDevice.ConnectionStatusChanged += OnConnectionStatusChanged;
            _connectedDevices[address] = btDevice;

            if (btDevice.ConnectionStatus == BluetoothConnectionStatus.Connected)
            {
                _logger.LogInformation("Already connected to Bluetooth device {Address:X12}", address);
                return true;
            }

            var rfcommServices = await btDevice.GetRfcommServicesAsync(BluetoothCacheMode.Cached).AsTask(ct).ConfigureAwait(false);
            if (rfcommServices.Services.Count > 0)
            {
                _logger.LogInformation("Connected to Bluetooth device {Address:X12}", address);
                return true;
            }

            _logger.LogWarning("No RFCOMM services found for device {Address:X12}", address);
            return btDevice.ConnectionStatus == BluetoothConnectionStatus.Connected;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Connection to Bluetooth device {Address:X12} was cancelled", address);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to Bluetooth device {Address:X12}", address);
            return false;
        }
    }

    public async Task<bool> DisconnectAsync(ulong address, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Disconnecting from Bluetooth device {Address:X12}...", address);

            if (!_connectedDevices.TryGetValue(address, out var btDevice))
            {
                _logger.LogWarning("Bluetooth device {Address:X12} is not managed by this service", address);
                return false;
            }

            btDevice.ConnectionStatusChanged -= OnConnectionStatusChanged;
            _connectedDevices.Remove(address);
            btDevice.Dispose();

            _logger.LogInformation("Disconnected from Bluetooth device {Address:X12}", address);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to disconnect from Bluetooth device {Address:X12}", address);
            return false;
        }
    }

    private async Task<List<BluetoothAudioDevice>> GetPairedDevicesInternalAsync(CancellationToken ct)
    {
        var devices = new List<BluetoothAudioDevice>();

        try
        {
            var selector = BluetoothDevice.GetDeviceSelector();
            var deviceInfos = await DeviceInformation.FindAllAsync(selector).AsTask(ct).ConfigureAwait(false);

            var tasks = deviceInfos.Select(async deviceInfo =>
            {
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

                try
                {
                    var btDevice = await BluetoothDevice.FromIdAsync(deviceInfo.Id).AsTask(linkedCts.Token).ConfigureAwait(false);
                    if (btDevice is null) return null;

                    var device = new BluetoothAudioDevice
                    {
                        Id = deviceInfo.Id,
                        Name = deviceInfo.Name,
                        IsConnected = btDevice.ConnectionStatus == BluetoothConnectionStatus.Connected,
                        IsPaired = btDevice.DeviceInformation.Pairing?.IsPaired ?? false,
                        DeviceType = DetectDeviceType(deviceInfo.Name),
                        BluetoothAddress = btDevice.BluetoothAddress,
                        LastSeenAt = DateTime.UtcNow
                    };

                    btDevice.Dispose();
                    return device;
                }
                catch (OperationCanceledException)
                {
                    return new BluetoothAudioDevice
                    {
                        Id = deviceInfo.Id,
                        Name = deviceInfo.Name,
                        IsConnected = false,
                        IsPaired = true,
                        DeviceType = DetectDeviceType(deviceInfo.Name),
                        BluetoothAddress = 0,
                        LastSeenAt = DateTime.UtcNow
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get info for paired device {DeviceId}", deviceInfo.Id);
                    return null;
                }
            }).ToList();

            var results = await Task.WhenAll(tasks).ConfigureAwait(false);

            foreach (var device in results)
            {
                if (device != null)
                    devices.Add(device);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enumerate paired Bluetooth devices");
        }

        return devices;
    }

    private void OnAdvertisementReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
    {
        try
        {
            var address = args.BluetoothAddress;
            var name = args.Advertisement.LocalName;
            if (string.IsNullOrEmpty(name))
            {
                name = $"Bluetooth Device {address:X12}";
            }

            var device = new BluetoothAudioDevice
            {
                Id = $"Bluetooth#{address:X12}",
                Name = name,
                IsConnected = false,
                IsPaired = false,
                DeviceType = DetectDeviceType(name),
                BluetoothAddress = address,
                LastSeenAt = DateTime.UtcNow
            };

            _discoveredDevices[address] = device;

            DeviceDiscovered?.Invoke(this, new BluetoothDeviceEventArgs { Device = device });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error processing Bluetooth advertisement");
        }
    }

    private void OnConnectionStatusChanged(BluetoothDevice sender, object args)
    {
        try
        {
            var address = sender.BluetoothAddress;
            var isConnected = sender.ConnectionStatus == BluetoothConnectionStatus.Connected;

            var device = new BluetoothAudioDevice
            {
                Id = sender.DeviceId,
                Name = sender.Name,
                IsConnected = isConnected,
                IsPaired = sender.DeviceInformation.Pairing?.IsPaired ?? false,
                DeviceType = DetectDeviceType(sender.Name),
                BluetoothAddress = address,
                LastSeenAt = DateTime.UtcNow
            };

            if (isConnected)
            {
                _logger.LogInformation("Bluetooth device {Address:X12} connected", address);
                DeviceConnected?.Invoke(this, new BluetoothDeviceEventArgs { Device = device });
            }
            else
            {
                _logger.LogInformation("Bluetooth device {Address:X12} disconnected", address);
                DeviceDisconnected?.Invoke(this, new BluetoothDeviceEventArgs { Device = device });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error processing Bluetooth connection status change");
        }
    }

    private static BluetoothDeviceType DetectDeviceType(string name)
    {
        var lowerName = name.ToLowerInvariant();

        if (lowerName.Contains("headset"))
            return BluetoothDeviceType.Headset;
        if (lowerName.Contains("speaker"))
            return BluetoothDeviceType.Speaker;

        return BluetoothDeviceType.Unknown;
    }

    public async Task StartMonitoringAsync(CancellationToken ct = default)
    {
        if (_isMonitoring)
        {
            _logger.LogInformation("Bluetooth monitoring is already active");
            return;
        }

        try
        {
            _logger.LogInformation("Starting Bluetooth device monitoring...");

            _adapter = await BluetoothAdapter.GetDefaultAsync().AsTask(ct).ConfigureAwait(false);
            if (_adapter is null)
            {
                _logger.LogWarning("No Bluetooth adapter found for monitoring");
                return;
            }

            var pairedDevices = await GetPairedDevicesInternalAsync(ct).ConfigureAwait(false);
            foreach (var device in pairedDevices)
            {
                _monitoredDeviceLastSeen[device.BluetoothAddress] = DateTime.UtcNow;
                if (device.IsConnected)
                {
                    DeviceDiscovered?.Invoke(this, new BluetoothDeviceEventArgs { Device = device });
                }
            }

            _monitorWatcher = new BluetoothLEAdvertisementWatcher
            {
                ScanningMode = BluetoothLEScanningMode.Passive
            };

            _monitorWatcher.Received += OnMonitorAdvertisementReceived;
            _monitorWatcher.Start();

            _monitorTimer = new PeriodicTimer(TimeSpan.FromSeconds(10));
            _isMonitoring = true;

            _ = RunMonitorLoopAsync(ct);

            _logger.LogInformation("Bluetooth device monitoring started");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Bluetooth monitoring startup was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start Bluetooth device monitoring");
        }
    }

    private async Task RunMonitorLoopAsync(CancellationToken ct)
    {
        try
        {
            while (_isMonitoring && _monitorTimer != null)
            {
                await _monitorTimer.WaitForNextTickAsync(ct).ConfigureAwait(false);

                if (!_isMonitoring) break;

                var now = DateTime.UtcNow;
                var expiredAddresses = _monitoredDeviceLastSeen
                    .Where(kvp => (now - kvp.Value).TotalSeconds > 30)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var address in expiredAddresses)
                {
                    _monitoredDeviceLastSeen.Remove(address);

                    var device = new BluetoothAudioDevice
                    {
                        Id = $"Bluetooth#{address:X12}",
                        Name = $"Bluetooth Device {address:X12}",
                        IsConnected = false,
                        IsPaired = false,
                        DeviceType = BluetoothDeviceType.Unknown,
                        BluetoothAddress = address,
                        LastSeenAt = now
                    };

                    _logger.LogInformation("Bluetooth device {Address:X12} signal lost", address);
                    DeviceDisconnected?.Invoke(this, new BluetoothDeviceEventArgs { Device = device });
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Bluetooth monitor loop");
        }
    }

    public Task StopMonitoringAsync()
    {
        if (!_isMonitoring) return Task.CompletedTask;

        _isMonitoring = false;
        _monitorTimer?.Dispose();
        _monitorTimer = null;

        if (_monitorWatcher != null)
        {
            _monitorWatcher.Received -= OnMonitorAdvertisementReceived;
            _monitorWatcher.Stop();
            _monitorWatcher = null;
        }

        _monitoredDeviceLastSeen.Clear();

        _logger.LogInformation("Bluetooth device monitoring stopped");
        return Task.CompletedTask;
    }

    private void OnMonitorAdvertisementReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
    {
        try
        {
            var address = args.BluetoothAddress;
            _monitoredDeviceLastSeen[address] = DateTime.UtcNow;

            var name = args.Advertisement.LocalName;
            if (string.IsNullOrEmpty(name))
            {
                name = $"Bluetooth Device {address:X12}";
            }

            var device = new BluetoothAudioDevice
            {
                Id = $"Bluetooth#{address:X12}",
                Name = name,
                IsConnected = false,
                IsPaired = false,
                DeviceType = DetectDeviceType(name),
                BluetoothAddress = address,
                LastSeenAt = DateTime.UtcNow
            };

            DeviceDiscovered?.Invoke(this, new BluetoothDeviceEventArgs { Device = device });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error processing monitoring advertisement");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        if (_isMonitoring)
        {
            _isMonitoring = false;
            _monitorTimer?.Dispose();
            _monitorTimer = null;

            if (_monitorWatcher != null)
            {
                _monitorWatcher.Received -= OnMonitorAdvertisementReceived;
                _monitorWatcher.Stop();
                _monitorWatcher = null;
            }

            _monitoredDeviceLastSeen.Clear();
        }

        _watcher?.Stop();
        _watcher = null;

        foreach (var device in _connectedDevices.Values)
        {
            try
            {
                device.ConnectionStatusChanged -= OnConnectionStatusChanged;
                device.Dispose();
            }
            catch
            {
            }
        }

        _connectedDevices.Clear();
        _discoveredDevices.Clear();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
