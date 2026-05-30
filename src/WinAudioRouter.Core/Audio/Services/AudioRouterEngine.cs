using Microsoft.Extensions.Logging;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using WinAudioRouter.Core.Audio.Events;
using WinAudioRouter.Core.Audio.Models;
using WinAudioRouter.Native.Wrappers;

namespace WinAudioRouter.Core.Audio.Services;

public sealed class AudioRouterEngine : IAudioRouterEngine, IDisposable
{
    private const int MaxTargets = 5;

    private readonly IAudioCaptureService _captureService;
    private readonly ILogger<AudioRouterEngine> _logger;

    private WasapiLoopbackCapture? _capture;
    private MMDevice? _sourceDevice;
    private readonly List<TargetPipeline> _pipelines = [];

    private bool _isRouting;
    private string? _sourceDeviceId;
    private int _captureLatencyMs;
    private bool _disposed;
    private bool _userRequestedStop;
    private int _retryCount;

    public int MaxRetryCount { get; set; } = 3;
    public int RetryDelayMs { get; set; } = 1000;

    public int CaptureLatencyMs
    {
        get => _captureLatencyMs;
        set => _captureLatencyMs = Math.Clamp(value, 0, 2000);
    }

    public bool IsRouting => _isRouting;
    public string? SourceDeviceId => _sourceDeviceId;
    public IReadOnlyList<RoutingTarget> ActiveTargets { get; private set; } = [];

    public event EventHandler<RoutingStateChangedEventArgs>? RoutingStateChanged;

    public AudioRouterEngine(IAudioCaptureService captureService, ILogger<AudioRouterEngine> logger)
    {
        _captureService = captureService;
        _logger = logger;
    }

    public Task StartRoutingAsync(string sourceDeviceId, IReadOnlyList<RoutingTarget> targets, int captureLatencyMs = 0, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_isRouting)
            throw new InvalidOperationException("路由已在进行中");

        if (targets.Count == 0)
            throw new ArgumentException("至少需要一个目标设备");

        if (targets.Count > MaxTargets)
            throw new ArgumentException($"最多支持 {MaxTargets} 个目标设备");

        _sourceDeviceId = sourceDeviceId;
        ActiveTargets = targets;
        _captureLatencyMs = Math.Clamp(captureLatencyMs, 0, 2000);
        _userRequestedStop = false;
        _retryCount = 0;

        StartRoutingCore();

        return Task.CompletedTask;
    }

    public Task StopRoutingAsync(CancellationToken cancellationToken = default)
    {
        if (!_isRouting) return Task.CompletedTask;

        _userRequestedStop = true;
        _isRouting = false;

        _capture?.StopRecording();
        foreach (var pipeline in _pipelines)
        {
            try { pipeline.Output?.Stop(); } catch { }
        }

        _logger.LogInformation("Audio routing stopped from {SourceDeviceId}", _sourceDeviceId);

        _sourceDeviceId = null;
        ActiveTargets = [];

        CleanupResources();
        OnRoutingStateChanged();

        return Task.CompletedTask;
    }

    public Task UpdateTargetLatencyAsync(string targetDeviceId, int latencyMs)
    {
        if (!_isRouting) return Task.CompletedTask;

        latencyMs = Math.Clamp(latencyMs, 0, 2000);

        var pipeline = _pipelines.FirstOrDefault(p => p.Target.DeviceId == targetDeviceId);
        if (pipeline == null || pipeline.CurrentLatencyMs == latencyMs) return Task.CompletedTask;

        _logger.LogInformation("Updating target {DeviceName} latency from {Old}ms to {New}ms",
            pipeline.Target.DeviceName, pipeline.CurrentLatencyMs, latencyMs);

        try
        {
            try { pipeline.Output?.Stop(); } catch { }
            try { pipeline.Output?.Dispose(); } catch { }

            var mmDevice = _captureService.GetDeviceById(targetDeviceId);
            if (mmDevice == null) return Task.CompletedTask;

            var effectiveLatency = latencyMs > 0 ? latencyMs : 50;
            var newOutput = new WasapiOut(mmDevice, AudioClientShareMode.Shared, true, effectiveLatency);

            var oldRingBuffer = pipeline.RingBuffer;
            int ringBufferSize = effectiveLatency * 48 * 4 * 2;
            var newRingBuffer = new LockFreeRingBuffer(ringBufferSize, _capture!.WaveFormat.BlockAlign);

            var waveProvider = new RingBufferWaveProvider(newRingBuffer, _capture.WaveFormat);
            newOutput.Init(waveProvider);
            newOutput.Play();

            try { oldRingBuffer?.Dispose(); } catch { }

            pipeline.Output = newOutput;
            pipeline.RingBuffer = newRingBuffer;
            pipeline.CurrentLatencyMs = latencyMs;

            _logger.LogInformation("Target {DeviceName} latency updated to {Latency}ms (ring buffer: {BufSize})",
                pipeline.Target.DeviceName, latencyMs, ringBufferSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update latency for target {DeviceName}", pipeline.Target.DeviceName);
        }

        return Task.CompletedTask;
    }

    public Task UpdateCaptureLatencyAsync(int latencyMs)
    {
        _captureLatencyMs = Math.Clamp(latencyMs, 0, 2000);
        _logger.LogInformation("Capture latency updated to {LatencyMs}ms", _captureLatencyMs);
        return Task.CompletedTask;
    }

    public bool UpdateTargetVolume(string targetDeviceId, float volume)
    {
        if (!_isRouting) return false;

        var pipeline = _pipelines.FirstOrDefault(p => p.Target.DeviceId == targetDeviceId);
        if (pipeline?.Output == null) return false;

        try
        {
            pipeline.Output.Volume = volume;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void StartRoutingCore()
    {
        _sourceDevice = _captureService.GetDeviceById(_sourceDeviceId!);
        _capture = new WasapiLoopbackCapture(_sourceDevice);

        _capture.DataAvailable += OnCaptureDataAvailable;
        _capture.RecordingStopped += OnCaptureStopped;

        foreach (var target in ActiveTargets)
        {
            _pipelines.Add(CreatePipeline(target));
        }

        _capture.StartRecording();

        foreach (var pipeline in _pipelines)
        {
            try
            {
                pipeline.Output?.Play();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start playback for {DeviceName}", pipeline.Target.DeviceName);
            }
        }

        _isRouting = true;
        _logger.LogInformation(
            "Audio routing started: source={SourceId}, targets={TargetCount}, captureLatency={CaptureLatency}ms",
            _sourceDeviceId, _pipelines.Count, _captureLatencyMs);
        OnRoutingStateChanged();
    }

    private TargetPipeline CreatePipeline(RoutingTarget target)
    {
        var mmDevice = _captureService.GetDeviceById(target.DeviceId);
        if (mmDevice == null)
            throw new InvalidOperationException($"Target device not found: {target.DeviceId}");

        var effectiveLatency = target.LatencyMs > 0 ? target.LatencyMs : 50;
        var output = new WasapiOut(mmDevice, AudioClientShareMode.Shared, true, effectiveLatency);

        int ringBufferSize = effectiveLatency * 48 * 4 * 2;
        var ringBuffer = new LockFreeRingBuffer(ringBufferSize, _capture!.WaveFormat.BlockAlign);

        var waveProvider = new RingBufferWaveProvider(ringBuffer, _capture.WaveFormat);
        output.Init(waveProvider);

        try
        {
            output.Volume = target.VolumeLevel / 100f;
        }
        catch { }

        return new TargetPipeline
        {
            Target = target,
            Output = output,
            RingBuffer = ringBuffer,
            CurrentLatencyMs = target.LatencyMs
        };
    }

    private void OnCaptureDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded == 0) return;

        foreach (var pipeline in _pipelines)
        {
            try
            {
                pipeline.RingBuffer?.Write(new ReadOnlySpan<byte>(e.Buffer, 0, e.BytesRecorded));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Write ring buffer failed for {DeviceName}", pipeline.Target.DeviceName);
            }
        }
    }

    private async void OnCaptureStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception != null)
            _logger.LogError(e.Exception, "Audio capture stopped unexpectedly");

        if (!_isRouting || _userRequestedStop) return;

        if (_retryCount < MaxRetryCount)
        {
            _retryCount++;
            _logger.LogWarning("Attempting reconnect {RetryCount}/{MaxRetry}", _retryCount, MaxRetryCount);
            await Task.Delay(RetryDelayMs);

            if (_userRequestedStop || _disposed) return;

            try
            {
                CleanupResources();
                StartRoutingCore();
                _retryCount = 0;
                _logger.LogInformation("Reconnect successful");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Reconnect attempt {RetryCount} failed", _retryCount);
                OnCaptureStopped(sender, e);
            }
        }
        else
        {
            _logger.LogError("Max retry count exceeded, stopping routing");
            _isRouting = false;
            _sourceDeviceId = null;
            ActiveTargets = [];
            CleanupResources();
            OnRoutingStateChanged();
        }
    }

    private void CleanupResources()
    {
        _capture?.Dispose();
        _capture = null;
        _sourceDevice = null;

        foreach (var pipeline in _pipelines)
        {
            try { pipeline.Output?.Dispose(); } catch { }
            pipeline.RingBuffer?.Dispose();
        }
        _pipelines.Clear();
    }

    private void OnRoutingStateChanged()
    {
        RoutingStateChanged?.Invoke(this, new RoutingStateChangedEventArgs
        {
            IsRouting = _isRouting,
            SourceDeviceId = _sourceDeviceId
        });
    }

    public void Dispose()
    {
        if (_disposed) return;

        if (_isRouting)
        {
            _userRequestedStop = true;
            _capture?.StopRecording();
            foreach (var pipeline in _pipelines)
            {
                try { pipeline.Output?.Stop(); } catch { }
            }
            _isRouting = false;
        }

        CleanupResources();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private class TargetPipeline
    {
        public RoutingTarget Target { get; init; } = null!;
        public WasapiOut? Output { get; set; }
        public LockFreeRingBuffer? RingBuffer { get; set; }
        public int CurrentLatencyMs { get; set; }
    }
}
