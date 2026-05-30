using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using WinAudioRouter.Core.Audio.Models;
using WinAudioRouter.Core.Audio.Services;
using WinAudioRouter.Core.Bluetooth.Events;
using WinAudioRouter.Core.Bluetooth.Models;
using WinAudioRouter.Core.Bluetooth.Services;

namespace WinAudioRouter.App.ViewModels;

public partial class BluetoothViewModel : ObservableObject
{
    private readonly IBluetoothService _bluetoothService;
    private readonly IAudioRouterEngine _routerEngine;
    private readonly ILogger<BluetoothViewModel> _logger;

    [ObservableProperty]
    private IReadOnlyList<BluetoothAudioDevice> _discoveredDevices = [];

    [ObservableProperty]
    private BluetoothAudioDevice? _selectedDevice;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private bool _isConnecting;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    public BluetoothViewModel(
        IBluetoothService bluetoothService,
        IAudioRouterEngine routerEngine,
        ILogger<BluetoothViewModel> logger)
    {
        _bluetoothService = bluetoothService;
        _routerEngine = routerEngine;
        _logger = logger;
        _bluetoothService.DeviceDiscovered += OnDeviceDiscovered;
        _bluetoothService.DeviceConnected += OnDeviceConnected;
        _bluetoothService.DeviceDisconnected += OnDeviceDisconnected;
    }

    [RelayCommand]
    private async Task ScanDevicesAsync()
    {
        try
        {
            IsScanning = true;
            StatusMessage = "Scanning for Bluetooth devices...";
            _logger.LogInformation("Starting Bluetooth device scan");

            var devices = await _bluetoothService.DiscoverDevicesAsync();
            DiscoveredDevices = devices;

            StatusMessage = devices.Count > 0
                ? $"Found {devices.Count} device(s)"
                : "No Bluetooth devices found";
            _logger.LogInformation("Bluetooth scan completed, found {Count} devices", devices.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scan Bluetooth devices");
            StatusMessage = "Scan failed";
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand]
    private async Task PairDeviceAsync(BluetoothAudioDevice device)
    {
        if (device == null) return;

        try
        {
            IsConnecting = true;
            StatusMessage = $"Pairing {device.Name}...";
            _logger.LogInformation("Pairing Bluetooth device {DeviceName}", device.Name);

            var success = await _bluetoothService.PairDeviceAsync(device.BluetoothAddress);
            StatusMessage = success
                ? $"Paired with {device.Name}"
                : $"Failed to pair {device.Name}";

            if (success)
            {
                await RefreshDeviceListAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to pair Bluetooth device {DeviceName}", device.Name);
            StatusMessage = $"Pairing error: {device.Name}";
        }
        finally
        {
            IsConnecting = false;
        }
    }

    [RelayCommand]
    private async Task ConnectDeviceAsync(BluetoothAudioDevice device)
    {
        if (device == null) return;

        try
        {
            IsConnecting = true;
            StatusMessage = $"Connecting to {device.Name}...";
            _logger.LogInformation("Connecting Bluetooth device {DeviceName}", device.Name);

            var success = await _bluetoothService.ConnectAsync(device.BluetoothAddress);
            StatusMessage = success
                ? $"Connected to {device.Name}"
                : $"Failed to connect {device.Name}";

            if (success)
            {
                await RefreshDeviceListAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect Bluetooth device {DeviceName}", device.Name);
            StatusMessage = $"Connection error: {device.Name}";
        }
        finally
        {
            IsConnecting = false;
        }
    }

    [RelayCommand]
    private async Task DisconnectDeviceAsync(BluetoothAudioDevice device)
    {
        if (device == null) return;

        try
        {
            StatusMessage = $"Disconnecting from {device.Name}...";
            _logger.LogInformation("Disconnecting Bluetooth device {DeviceName}", device.Name);

            var success = await _bluetoothService.DisconnectAsync(device.BluetoothAddress);
            StatusMessage = success
                ? $"Disconnected from {device.Name}"
                : $"Failed to disconnect {device.Name}";

            if (success)
            {
                await RefreshDeviceListAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to disconnect Bluetooth device {DeviceName}", device.Name);
            StatusMessage = $"Disconnection error: {device.Name}";
        }
    }

    [RelayCommand]
    private async Task RouteToBluetoothDeviceAsync(BluetoothAudioDevice device)
    {
        if (device == null) return;

        try
        {
            if (!device.IsConnected)
            {
                StatusMessage = $"Connecting to {device.Name} first...";
                var connected = await _bluetoothService.ConnectAsync(device.BluetoothAddress);
                if (!connected)
                {
                    StatusMessage = $"Cannot connect to {device.Name}";
                    return;
                }
                await RefreshDeviceListAsync();
            }

            StatusMessage = $"Routing audio to {device.Name}...";
            _logger.LogInformation("Routing audio to Bluetooth device {DeviceName}", device.Name);

            var target = new RoutingTarget(device.Id, device.Name, 100, 100);
            await _routerEngine.StartRoutingAsync(_routerEngine.SourceDeviceId ?? string.Empty, [target]);
            StatusMessage = $"Routing to {device.Name}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to route audio to Bluetooth device {DeviceName}", device.Name);
            StatusMessage = $"Routing error: {device.Name}";
        }
    }

    private void OnDeviceDiscovered(object? sender, BluetoothDeviceEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var list = DiscoveredDevices.ToList();
            var existingIndex = list.FindIndex(d => d.BluetoothAddress == e.Device.BluetoothAddress);
            if (existingIndex >= 0)
                list[existingIndex] = e.Device;
            else
                list.Add(e.Device);

            DiscoveredDevices = list.AsReadOnly();
        });
    }

    private void OnDeviceConnected(object? sender, BluetoothDeviceEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var list = DiscoveredDevices.ToList();
            var index = list.FindIndex(d => d.BluetoothAddress == e.Device.BluetoothAddress);
            if (index >= 0)
            {
                list[index] = e.Device;
                DiscoveredDevices = list.AsReadOnly();
            }

            StatusMessage = $"{e.Device.Name} connected";
        });
    }

    private void OnDeviceDisconnected(object? sender, BluetoothDeviceEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var list = DiscoveredDevices.ToList();
            var index = list.FindIndex(d => d.BluetoothAddress == e.Device.BluetoothAddress);
            if (index >= 0)
            {
                list[index] = e.Device;
                DiscoveredDevices = list.AsReadOnly();
            }

            StatusMessage = $"{e.Device.Name} disconnected";
        });
    }

    private async Task RefreshDeviceListAsync()
    {
        try
        {
            var devices = await _bluetoothService.DiscoverDevicesAsync();
            DiscoveredDevices = devices;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh Bluetooth device list");
        }
    }
}
