using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using WinAudioRouter.Core.Bluetooth.Models;
using WinAudioRouter.Core.Bluetooth.Services;
using WinAudioRouter.Core.Configuration;

namespace WinAudioRouter.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IBluetoothSettingsService _bluetoothSettingsService;
    private readonly ILogger<SettingsViewModel> _logger;
    private AppConfiguration _configuration;

    [ObservableProperty]
    private bool _isAutoReconnectEnabled = true;

    [ObservableProperty]
    private double _scanTimeoutSeconds = 10;

    [ObservableProperty]
    private double _reconnectIntervalSeconds = 30;

    [ObservableProperty]
    private string _latencyMode = "Normal";

    [ObservableProperty]
    private IReadOnlyList<BluetoothAudioDevice> _savedDevices = [];

    [ObservableProperty]
    private bool _isLoading;

    public SettingsViewModel(
        IBluetoothSettingsService bluetoothSettingsService,
        ILogger<SettingsViewModel> logger)
    {
        _bluetoothSettingsService = bluetoothSettingsService;
        _logger = logger;
        _configuration = AppConfiguration.Default;
    }

    partial void OnIsAutoReconnectEnabledChanged(bool value)
    {
        _bluetoothSettingsService.IsAutoReconnectEnabled = value;
        _ = SaveConfigurationAsync();
    }

    partial void OnScanTimeoutSecondsChanged(double value)
    {
        _ = SaveConfigurationAsync();
    }

    partial void OnReconnectIntervalSecondsChanged(double value)
    {
        _ = SaveConfigurationAsync();
    }

    partial void OnLatencyModeChanged(string value)
    {
        _ = SaveConfigurationAsync();
    }

    [RelayCommand]
    private async Task LoadSettingsAsync()
    {
        try
        {
            IsLoading = true;
            _configuration = await AppConfiguration.LoadAsync();

            IsAutoReconnectEnabled = _configuration.Bluetooth.AutoReconnectEnabled;
            ScanTimeoutSeconds = _configuration.Bluetooth.ScanTimeoutSeconds;
            ReconnectIntervalSeconds = _configuration.Bluetooth.ReconnectIntervalSeconds;
            LatencyMode = _configuration.LatencyMode;

            _bluetoothSettingsService.IsAutoReconnectEnabled = IsAutoReconnectEnabled;
            SavedDevices = await _bluetoothSettingsService.GetSavedDevicesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load settings");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RemoveDeviceAsync(BluetoothAudioDevice device)
    {
        if (device == null) return;

        try
        {
            await _bluetoothSettingsService.RemoveDeviceAsync(device.Id);
            SavedDevices = await _bluetoothSettingsService.GetSavedDevicesAsync();
            _logger.LogInformation("Removed device {DeviceName}", device.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove device {DeviceName}", device.Name);
        }
    }

    private async Task SaveConfigurationAsync()
    {
        try
        {
            _configuration.Bluetooth.AutoReconnectEnabled = IsAutoReconnectEnabled;
            _configuration.Bluetooth.ScanTimeoutSeconds = (int)ScanTimeoutSeconds;
            _configuration.Bluetooth.ReconnectIntervalSeconds = (int)ReconnectIntervalSeconds;
            _configuration.LatencyMode = LatencyMode;

            await _configuration.SaveAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save configuration");
        }
    }
}
