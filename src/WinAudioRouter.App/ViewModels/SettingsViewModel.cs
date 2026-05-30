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
    private double _fontSize = 15;

    [ObservableProperty]
    private string _selectedLanguage = "简体中文";

    [ObservableProperty]
    private bool _isAutoStartEnabled;

    [ObservableProperty]
    private IReadOnlyList<BluetoothAudioDevice> _savedDevices = [];

    public string FontSizeDisplay => $"{FontSize:F0}px";

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

    partial void OnFontSizeChanged(double value)
    {
        OnPropertyChanged(nameof(FontSizeDisplay));
        _ = SaveConfigurationAsync();
    }

    partial void OnSelectedLanguageChanged(string value)
    {
        _ = SaveConfigurationAsync();
    }

    partial void OnIsAutoStartEnabledChanged(bool value)
    {
        SetAutoStart(value);
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
            FontSize = _configuration.FontSize;
            SelectedLanguage = _configuration.Language ?? "简体中文";
            IsAutoStartEnabled = CheckAutoStart();

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
            _configuration.FontSize = (int)FontSize;
            _configuration.Language = SelectedLanguage;

            await _configuration.SaveAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save configuration");
        }
    }

    private static bool CheckAutoStart()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false);
            var val = key?.GetValue("WinAudioRouter");
            return val != null;
        }
        catch { return false; }
    }

    private static void SetAutoStart(bool enable)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            if (enable)
            {
                var exePath = Environment.ProcessPath!;
                key.SetValue("WinAudioRouter", $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue("WinAudioRouter", false);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Settings] Auto-start error: {ex.Message}");
        }
    }
}
