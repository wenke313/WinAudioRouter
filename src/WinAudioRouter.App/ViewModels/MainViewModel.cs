using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using WinAudioRouter.Core.Audio.Events;
using WinAudioRouter.Core.Audio.Models;
using WinAudioRouter.Core.Audio.Services;
using WinAudioRouter.Core.Configuration;

namespace WinAudioRouter.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private const int MaxTargets = 5;

    private readonly IAudioDeviceManager _audioDeviceManager;
    private readonly IAudioRouterEngine _routerEngine;
    private readonly ILogger<MainViewModel> _logger;
    private AppConfiguration _config;

    private bool _isRefreshing;
    private CancellationTokenSource? _captureLatencyCts;
    private readonly Dictionary<string, CancellationTokenSource> _targetLatencyCts = [];

    [ObservableProperty]
    private string _title = "🔊 音频路由器";

    [ObservableProperty]
    private IReadOnlyList<AudioDevice> _devices = [];

    [ObservableProperty]
    private AudioDevice? _selectedDevice;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private AudioDevice? _sourceDevice;

    [ObservableProperty]
    private bool _isRouting;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private int _captureLatencyMs;

    [ObservableProperty]
    private int _sourceVolume;

    public ObservableCollection<RoutingTarget> RoutingTargets { get; } = [];

    public string CaptureLatencyLabel => CaptureLatencyMs == 0 ? "捕获延迟: 默认" : $"捕获延迟: {CaptureLatencyMs}ms";

    public string SourceLatencyInfo => SourceDevice != null
        ? $"系统延迟: 默认 {SourceDevice.DefaultPeriodMs:F1}ms / 最低 {SourceDevice.MinimumPeriodMs:F1}ms"
        : "";

    public bool CanAddTarget => RoutingTargets.Count < MaxTargets;
    public bool CanStartRouting => SourceDevice != null && RoutingTargets.Count > 0 && !IsRouting;

    public MainViewModel(
        IAudioDeviceManager audioDeviceManager,
        IAudioRouterEngine routerEngine,
        ILogger<MainViewModel> logger)
    {
        _audioDeviceManager = audioDeviceManager;
        _routerEngine = routerEngine;
        _logger = logger;
        _config = AppConfiguration.Default;
        _routerEngine.RoutingStateChanged += OnRoutingStateChanged;
        _audioDeviceManager.DeviceChanged += OnDeviceChanged;
        _audioDeviceManager.DefaultDeviceChanged += OnDefaultDeviceChanged;
        RoutingTargets.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(CanAddTarget));
            OnPropertyChanged(nameof(CanStartRouting));
            _ = SaveConfigAsync();
        };
    }

    partial void OnSourceVolumeChanged(int value)
    {
        if (SourceDevice == null) return;
        SourceDevice.VolumeLevel = value;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(150);
                await _audioDeviceManager.SetDeviceVolumeAsync(SourceDevice.Id, value);
            }
            catch { }
        });

        _ = SaveConfigAsync();
    }

    partial void OnCaptureLatencyMsChanged(int value)
    {
        OnPropertyChanged(nameof(CaptureLatencyLabel));
        _ = SaveConfigAsync();
        if (!IsRouting) return;

        _captureLatencyCts?.Cancel();
        _captureLatencyCts = new CancellationTokenSource();
        var token = _captureLatencyCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(300, token);
                await _routerEngine.UpdateCaptureLatencyAsync(value);
            }
            catch (OperationCanceledException) { }
        }, token);
    }

    partial void OnSourceDeviceChanged(AudioDevice? value)
    {
        if (value != null)
        {
            SourceVolume = value.VolumeLevel;
        }
        OnPropertyChanged(nameof(CanStartRouting));
        OnPropertyChanged(nameof(SourceLatencyInfo));
        _ = SaveConfigAsync();
    }

    partial void OnIsRoutingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanStartRouting));
    }

    [RelayCommand]
    private async Task LoadDevicesAsync()
    {
        if (_isRefreshing) return;
        _isRefreshing = true;

        try
        {
            IsLoading = true;
            _isInitializing = true;
            _logger.LogInformation("Loading audio devices...");

            _config = await AppConfiguration.LoadAsync();
            _logger.LogInformation("Config loaded: source={SourceId}, captureLatency={Latency}ms, targets={Count}",
                _config.Routing.SourceDeviceId ?? "(null)", _config.Routing.CaptureLatencyMs,
                _config.Routing.Targets?.Count ?? 0);
            Devices = await _audioDeviceManager.GetPlaybackDevicesAsync();
            SelectedDevice = Devices.FirstOrDefault(d => d.IsDefault);

            var savedSourceId = _config.Routing.SourceDeviceId;
            SourceDevice = !string.IsNullOrEmpty(savedSourceId)
                ? Devices.FirstOrDefault(d => d.Id == savedSourceId) ?? Devices.FirstOrDefault(d => d.IsDefault)
                : Devices.FirstOrDefault(d => d.IsDefault);

            CaptureLatencyMs = _config.Routing.CaptureLatencyMs;

            foreach (var savedTarget in _config.Routing.Targets)
            {
                var device = Devices.FirstOrDefault(d => d.Id == savedTarget.DeviceId);
                if (device == null)
                {
                    _logger.LogWarning("Saved target device not found: {DeviceId} ({DeviceName}), skipping",
                        savedTarget.DeviceId, savedTarget.DeviceName);
                    continue;
                }
                if (RoutingTargets.Any(t => t.DeviceId == savedTarget.DeviceId)) continue;
                if (SourceDevice?.Id == savedTarget.DeviceId)
                {
                    _logger.LogWarning("Saved target is same as source device: {DeviceId}, skipping", savedTarget.DeviceId);
                    continue;
                }

                var target = new RoutingTarget(device.Id, device.Name,
                    savedTarget.VolumeLevel, savedTarget.LatencyMs,
                    device.DefaultPeriodMs, device.MinimumPeriodMs);
                target.PropertyChanged += OnTargetPropertyChanged;
                RoutingTargets.Add(target);
                _logger.LogInformation("Restored target: {DeviceName} vol={Vol}% latency={Latency}ms",
                    device.Name, savedTarget.VolumeLevel, savedTarget.LatencyMs);
            }

            _logger.LogInformation("Loaded {Count} audio devices, restored {TargetCount} targets",
                Devices.Count, RoutingTargets.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load audio devices");
        }
        finally
        {
            IsLoading = false;
            _isRefreshing = false;
            _isInitializing = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadDevicesAsync();

    [RelayCommand]
    private void AddTargetDevice(AudioDevice device)
    {
        if (device == null) return;
        if (RoutingTargets.Count >= MaxTargets) return;
        if (RoutingTargets.Any(t => t.DeviceId == device.Id)) return;
        if (SourceDevice?.Id == device.Id) return;

        var target = new RoutingTarget(device.Id, device.Name, device.VolumeLevel, 0,
            device.DefaultPeriodMs, device.MinimumPeriodMs);
        target.PropertyChanged += OnTargetPropertyChanged;
        RoutingTargets.Add(target);
        _logger.LogInformation("Added target device: {DeviceName}", device.Name);
    }

    [RelayCommand]
    private void RemoveTargetDevice(RoutingTarget target)
    {
        if (target == null) return;
        target.PropertyChanged -= OnTargetPropertyChanged;
        RoutingTargets.Remove(target);

        if (_targetLatencyCts.TryGetValue(target.DeviceId, out var cts))
        {
            cts.Cancel();
            _targetLatencyCts.Remove(target.DeviceId);
        }

        _logger.LogInformation("Removed target device: {DeviceName}", target.DeviceName);
    }

    private void OnTargetPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not RoutingTarget target) return;

        if (e.PropertyName is nameof(RoutingTarget.VolumeLevel) or nameof(RoutingTarget.LatencyMs))
        {
            _ = SaveConfigAsync();
        }

        if (e.PropertyName == nameof(RoutingTarget.VolumeLevel) && IsRouting)
        {
            _ = Task.Run(() =>
            {
                try
                {
                    _routerEngine.UpdateTargetVolume(target.DeviceId, target.VolumeLevel / 100f);
                }
                catch { }
            });
        }

        if (e.PropertyName == nameof(RoutingTarget.LatencyMs) && IsRouting)
        {
            if (_targetLatencyCts.TryGetValue(target.DeviceId, out var oldCts))
            {
                oldCts.Cancel();
            }

            var cts = new CancellationTokenSource();
            _targetLatencyCts[target.DeviceId] = cts;
            var token = cts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(300, token);
                    await _routerEngine.UpdateTargetLatencyAsync(target.DeviceId, target.LatencyMs);
                }
                catch (OperationCanceledException) { }
            }, token);
        }
    }

    [RelayCommand]
    private async Task StartRoutingAsync()
    {
        if (SourceDevice == null || RoutingTargets.Count == 0) return;

        try
        {
            _logger.LogInformation(
                "Starting audio routing from {Source} to {TargetCount} targets, captureLatency={Latency}ms",
                SourceDevice.Name, RoutingTargets.Count, CaptureLatencyMs);

            var targets = RoutingTargets.ToList();
            await _routerEngine.StartRoutingAsync(SourceDevice.Id, targets, CaptureLatencyMs);
            IsRouting = true;
            ErrorMessage = string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start audio routing");
            IsRouting = false;
            ErrorMessage = $"启动路由失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task StopRoutingAsync()
    {
        try
        {
            _logger.LogInformation("Stopping audio routing");
            await _routerEngine.StopRoutingAsync();
            IsRouting = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop audio routing");
        }
    }

    [RelayCommand]
    private void SelectSourceDevice(AudioDevice device)
    {
        if (device == null) return;
        SourceDevice = device;
        SourceVolume = device.VolumeLevel;
        _logger.LogInformation("Source device selected: {DeviceName}", device.Name);
    }

    [RelayCommand]
    private async Task SwitchDefaultDeviceAsync()
    {
        if (SelectedDevice == null) return;

        try
        {
            ErrorMessage = string.Empty;
            var success = await _audioDeviceManager.SetDefaultPlaybackDeviceAsync(SelectedDevice.Id);
            if (success)
            {
                await LoadDevicesAsync();
                SourceDevice = Devices.FirstOrDefault(d => d.IsDefault);
            }
            else
            {
                ErrorMessage = $"切换默认设备失败: {SelectedDevice.Name}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to switch default device");
            ErrorMessage = $"切换默认设备异常: {ex.Message}";
        }
    }

    public bool UpdateTargetVolume(string targetDeviceId, float volume)
    {
        return _routerEngine.UpdateTargetVolume(targetDeviceId, volume);
    }

    [RelayCommand]
    private async Task EditCaptureLatencyAsync()
    {
        var result = await Shell.Current.DisplayPromptAsync(
            "设置捕获延迟",
            SourceLatencyInfo + "\n\n输入 0 = 使用设备默认延迟",
            initialValue: CaptureLatencyMs.ToString(),
            keyboard: Keyboard.Numeric);

        if (result != null && int.TryParse(result, out var latency))
        {
            CaptureLatencyMs = Math.Clamp(latency, 0, 2000);
        }
    }

    [RelayCommand]
    private async Task EditTargetLatencyAsync(RoutingTarget target)
    {
        if (target == null) return;

        var result = await Shell.Current.DisplayPromptAsync(
            "设置延迟",
            $"设备: {target.DeviceName}\n{target.DeviceLatencyInfo}\n\n输入 0 = 使用设备默认延迟",
            initialValue: target.LatencyMs.ToString(),
            keyboard: Keyboard.Numeric);

        if (result != null && int.TryParse(result, out var latency))
        {
            target.LatencyMs = latency;
        }
    }

    [RelayCommand]
    private async Task NavigateToBluetoothAsync()
    {
        await Shell.Current.GoToAsync("BluetoothPage");
    }

    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private bool _savePending;
    private bool _isInitializing;

    private async Task SaveConfigAsync()
    {
        if (_isInitializing) return;
        if (_savePending) return;
        _savePending = true;

        try
        {
            await _saveLock.WaitAsync().ConfigureAwait(false);
            try
            {
                _config.Routing.SourceDeviceId = SourceDevice?.Id;
                _config.Routing.CaptureLatencyMs = CaptureLatencyMs;
                _config.Routing.Targets = RoutingTargets.Select(t => new TargetDeviceConfig
                {
                    DeviceId = t.DeviceId,
                    DeviceName = t.DeviceName,
                    VolumeLevel = t.VolumeLevel,
                    LatencyMs = t.LatencyMs
                }).ToList();

                await _config.SaveAsync().ConfigureAwait(false);
            }
            finally
            {
                _saveLock.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save configuration");
        }
        finally
        {
            _savePending = false;
        }
    }

    private void OnRoutingStateChanged(object? sender, RoutingStateChangedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            IsRouting = e.IsRouting;
        });
    }

    private void OnDeviceChanged(object? sender, DeviceChangedEventArgs e)
    {
        if (_isRefreshing) return;
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await LoadDevicesAsync();
        });
    }

    private void OnDefaultDeviceChanged(object? sender, DefaultDeviceChangedEventArgs e)
    {
        if (_isRefreshing) return;
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await LoadDevicesAsync();
        });
    }
}
