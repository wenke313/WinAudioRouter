using Microsoft.Extensions.Logging;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using WinAudioRouter.Core.Audio.Events;
using WinAudioRouter.Core.Audio.Models;

namespace WinAudioRouter.Core.Audio.Services;

public sealed class AudioCaptureService : IAudioCaptureService, IDisposable
{
    private readonly ILogger<AudioCaptureService> _logger;
    private readonly MMDeviceEnumerator _enumerator;
    private WasapiLoopbackCapture? _capture;
    private bool _disposed;

    private long _totalBytesCaptured;
    private int _captureCount;
    private DateTime? _startTime;

    public bool IsCapturing { get; private set; }

    public event EventHandler<AudioCapturedEventArgs>? AudioCaptured;

    public AudioCaptureService(ILogger<AudioCaptureService> logger)
    {
        _logger = logger;
        _enumerator = new MMDeviceEnumerator();
    }

    public MMDevice GetDeviceById(string deviceId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var device = _enumerator.GetDevice(deviceId);
        return device;
    }

    public WaveFormat GetDeviceWaveFormat(string deviceId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var device = _enumerator.GetDevice(deviceId);
        return device.AudioClient.MixFormat;
    }

    public Task StartCaptureAsync(string? deviceId = null, CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(AudioCaptureService));
        }

        if (IsCapturing)
        {
            throw new InvalidOperationException("Audio capture is already in progress");
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            if (deviceId is not null)
            {
                _logger.LogInformation("Starting audio capture for device {DeviceId}", deviceId);
                var device = _enumerator.GetDevice(deviceId);
                _capture = new WasapiLoopbackCapture(device);
            }
            else
            {
                _logger.LogInformation("Starting audio capture for default device");
                _capture = new WasapiLoopbackCapture();
            }

            _capture.DataAvailable += OnDataAvailable;
            _capture.RecordingStopped += OnRecordingStopped;

            _capture.StartRecording();
            IsCapturing = true;

            _totalBytesCaptured = 0;
            _captureCount = 0;
            _startTime = DateTime.UtcNow;

            _logger.LogInformation("Audio capture started successfully");
        }
        catch (Exception ex) when (ex is not ObjectDisposedException and not InvalidOperationException)
        {
            _logger.LogError(ex, "Failed to start audio capture for device {DeviceId}", deviceId);
            CleanupCapture();
            throw;
        }

        return Task.CompletedTask;
    }

    public Task StopCaptureAsync(CancellationToken cancellationToken = default)
    {
        if (!IsCapturing || _capture is null)
        {
            _logger.LogWarning("Audio capture is not in progress, ignoring stop request");
            return Task.CompletedTask;
        }

        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation("Stopping audio capture");
        _capture.StopRecording();

        return Task.CompletedTask;
    }

    public CaptureStatistics GetStatistics()
    {
        var elapsed = _startTime.HasValue ? (DateTime.UtcNow - _startTime.Value).TotalSeconds : 0;
        var avgBps = elapsed > 0 ? _totalBytesCaptured / elapsed : 0;

        return new CaptureStatistics
        {
            TotalBytesCaptured = _totalBytesCaptured,
            CaptureCount = _captureCount,
            StartTime = _startTime,
            AverageBytesPerSecond = avgBps
        };
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded == 0) return;

        _totalBytesCaptured += e.BytesRecorded;
        _captureCount++;

        var args = new AudioCapturedEventArgs
        {
            AudioData = new byte[e.BytesRecorded],
            Length = e.BytesRecorded,
            SamplingRate = _capture?.WaveFormat?.SampleRate ?? 44100,
            BitDepth = _capture?.WaveFormat?.BitsPerSample ?? 16
        };

        Buffer.BlockCopy(e.Buffer, 0, args.AudioData!, 0, e.BytesRecorded);

        AudioCaptured?.Invoke(this, args);
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        IsCapturing = false;

        if (e.Exception is not null)
        {
            _logger.LogError(e.Exception, "Audio capture stopped with error");
        }
        else
        {
            _logger.LogInformation("Audio capture stopped");
        }

        CleanupCapture();
    }

    private void CleanupCapture()
    {
        if (_capture is not null)
        {
            _capture.DataAvailable -= OnDataAvailable;
            _capture.RecordingStopped -= OnRecordingStopped;
            _capture.Dispose();
            _capture = null;
        }

        IsCapturing = false;
    }

    public void Dispose()
    {
        if (_disposed) return;

        CleanupCapture();
        _enumerator.Dispose();
        _disposed = true;

        GC.SuppressFinalize(this);
    }
}
