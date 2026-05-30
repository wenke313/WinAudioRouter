using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using WinAudioRouter.Native.Interop;

namespace WinAudioRouter.Native.Wrappers;

public sealed partial class WasapiLowLatencyRenderer : IDisposable
{
    private IntPtr _audioClientComPtr;
    private IAudioClient? _audioClient;
    private IAudioRenderClient? _renderClient;
    private IAudioStreamVolume? _streamVolume;

    private uint _bufferFrameCount;
    private int _frameSize;
    private int _sampleRate;
    private int _channels;
    private int _bitsPerSample;

    private SafeWaitHandle? _eventHandle;
    private Task? _renderTask;
    private CancellationTokenSource? _cts;
    private volatile bool _isPlaying;
    private bool _disposed;

    private float _volume = 1.0f;

    public bool IsPlaying => _isPlaying;
    public double LatencyMs => _sampleRate > 0 ? _bufferFrameCount * 1000.0 / _sampleRate : 0;
    public int SampleRate => _sampleRate;
    public int Channels => _channels;
    public int BitsPerSample => _bitsPerSample;
    public int FrameSize => _frameSize;
    public LockFreeRingBuffer? RingBuffer { get; set; }

    public Action<string>? LogInfo { get; set; }
    public Action<string>? LogWarn { get; set; }
    public Action<string>? LogError { get; set; }
    public Action<string>? LogDebug { get; set; }

    public float Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0f, 1f);
            ApplyVolume();
        }
    }

    public WasapiLowLatencyRenderer() { }

    public void Initialize(string deviceId, int desiredLatencyMs = 50)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            var clsidEnumerator = new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E");
            var iidEnumerator = WasapiGuids.IID_IMMDeviceEnumerator;

            int hr = CoCreateInstance(
                ref clsidEnumerator, IntPtr.Zero, 1,
                ref iidEnumerator, out IntPtr enumeratorPtr);

            if (hr != 0 || enumeratorPtr == IntPtr.Zero)
                throw new COMException($"CoCreateInstance(MMDeviceEnumerator) failed, hr=0x{hr:X8}", hr);

            try
            {
                var enumerator = (IMMDeviceEnumerator)Marshal.GetUniqueObjectForIUnknown(enumeratorPtr);

                hr = enumerator.GetDevice(deviceId, out IntPtr devicePtr);
                if (hr != 0 || devicePtr == IntPtr.Zero)
                    throw new COMException($"GetDevice('{deviceId}') failed, hr=0x{hr:X8}", hr);

                try
                {
                    var mmDevice = (IMMDevice)Marshal.GetUniqueObjectForIUnknown(devicePtr);

                    var iidAudioClient = WasapiGuids.IID_IAudioClient;
                    hr = mmDevice.Activate(ref iidAudioClient, 0, IntPtr.Zero, out IntPtr audioClientPtr);
                    if (hr != 0 || audioClientPtr == IntPtr.Zero)
                        throw new COMException($"IMMDevice.Activate(IAudioClient) failed, hr=0x{hr:X8}", hr);

                    _audioClientComPtr = audioClientPtr;
                    _audioClient = (IAudioClient)Marshal.GetUniqueObjectForIUnknown(audioClientPtr);
                }
                finally
                {
                    Marshal.Release(devicePtr);
                }
            }
            finally
            {
                Marshal.Release(enumeratorPtr);
            }

            InitializeAudioClient(desiredLatencyMs);
            GetRenderClient();
            GetStreamVolume();

            int ringBufferSize = (int)(_bufferFrameCount * _frameSize * 8);
            RingBuffer = new LockFreeRingBuffer(ringBufferSize, _frameSize);

            LogInfo?.Invoke($"WASAPI renderer initialized: latency={LatencyMs:F1}ms, bufferFrames={_bufferFrameCount}, sampleRate={_sampleRate}, channels={_channels}, bits={_bitsPerSample}, ringBuffer={ringBufferSize}");
        }
        catch (Exception ex)
        {
            LogError?.Invoke($"Failed to initialize WASAPI renderer for device {deviceId}: {ex}");
            CleanupResources();
            throw;
        }
    }

    private void InitializeAudioClient(int desiredLatencyMs)
    {
        int hr = _audioClient!.GetMixFormat(out IntPtr formatPtr);
        Marshal.ThrowExceptionForHR(hr);

        try
        {
            var format = Marshal.PtrToStructure<WAVEFORMATEX>(formatPtr);
            _sampleRate = (int)format.nSamplesPerSec;
            _channels = format.nChannels;
            _bitsPerSample = format.wBitsPerSample;
            _frameSize = format.nBlockAlign;
        }
        finally
        {
            CoTaskMemFree(formatPtr);
        }

        hr = _audioClient!.GetDevicePeriod(out long defaultPeriod, out long minimumPeriod);
        Marshal.ThrowExceptionForHR(hr);

        long desiredHns = Math.Max(desiredLatencyMs, 20) * AudClnt.HNS_PER_MS;
        long bufferDuration = Math.Max(desiredHns, defaultPeriod);

        _eventHandle = new SafeWaitHandle(CreateEvent(IntPtr.Zero, false, false, IntPtr.Zero), true);
        if (_eventHandle.IsInvalid)
            throw new InvalidOperationException("Failed to create event handle");

        hr = _audioClient!.GetMixFormat(out formatPtr);
        Marshal.ThrowExceptionForHR(hr);

        try
        {
            int streamFlags = AudClnt.STREAMFLAGS_EVENTCALLBACK
                              | AudClnt.STREAMFLAGS_SRC_DEFAULT_QUALITY;

            var sessionGuid = Guid.Empty;

            hr = _audioClient!.Initialize(
                AudClnt.SHAREMODE_SHARED,
                streamFlags,
                bufferDuration,
                0,
                formatPtr,
                ref sessionGuid);

            if (hr == AudClnt.E_BUFFER_SIZE_NOT_ALIGNED)
            {
                LogWarn?.Invoke("Buffer size not aligned, recalculating...");
                hr = _audioClient!.GetBufferSize(out _bufferFrameCount);
                Marshal.ThrowExceptionForHR(hr);

                bufferDuration = (long)_bufferFrameCount * AudClnt.HNS_PER_SECOND / _sampleRate;

                hr = _audioClient!.Initialize(
                    AudClnt.SHAREMODE_SHARED,
                    streamFlags,
                    bufferDuration,
                    0,
                    formatPtr,
                    ref sessionGuid);
            }

            Marshal.ThrowExceptionForHR(hr);
        }
        finally
        {
            CoTaskMemFree(formatPtr);
        }

        hr = _audioClient!.GetBufferSize(out _bufferFrameCount);
        Marshal.ThrowExceptionForHR(hr);

        hr = _audioClient!.SetEventHandle(_eventHandle.DangerousGetHandle());
        Marshal.ThrowExceptionForHR(hr);
    }

    private void GetRenderClient()
    {
        var iid = WasapiGuids.IID_IAudioRenderClient;
        int hr = _audioClient!.GetService(ref iid, out IntPtr ptr);
        Marshal.ThrowExceptionForHR(hr);

        _renderClient = (IAudioRenderClient)Marshal.GetUniqueObjectForIUnknown(ptr);
        Marshal.Release(ptr);
    }

    private void GetStreamVolume()
    {
        try
        {
            var iid = WasapiGuids.IID_IAudioStreamVolume;
            int hr = _audioClient!.GetService(ref iid, out IntPtr ptr);
            if (hr == 0)
            {
                _streamVolume = (IAudioStreamVolume)Marshal.GetUniqueObjectForIUnknown(ptr);
                Marshal.Release(ptr);
            }
        }
        catch
        {
            _streamVolume = null;
        }
    }

    private void ApplyVolume()
    {
        if (_streamVolume == null || _channels == 0) return;

        try
        {
            var levels = new float[_channels];
            for (int i = 0; i < _channels; i++)
                levels[i] = _volume;
            _streamVolume.SetAllVolumes((uint)_channels, levels);
        }
        catch
        {
        }
    }

    public void Play()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_isPlaying) return;

        _cts = new CancellationTokenSource();

        PreFillBuffer();

        int hr = _audioClient!.Start();
        Marshal.ThrowExceptionForHR(hr);

        _renderTask = Task.Run(() => RenderLoop(_cts.Token));
        _isPlaying = true;

        ApplyVolume();

        LogInfo?.Invoke("Audio rendering started");
    }

    public void Stop()
    {
        if (!_isPlaying) return;

        _cts?.Cancel();

        try
        {
            _audioClient?.Stop();
            _audioClient?.Reset();
        }
        catch
        {
        }

        _isPlaying = false;
        RingBuffer?.Reset();

        LogInfo?.Invoke("Audio rendering stopped");
    }

    private void PreFillBuffer()
    {
        int hr = _audioClient!.GetCurrentPadding(out int paddingFrames);
        Marshal.ThrowExceptionForHR(hr);

        uint framesAvailable = _bufferFrameCount - (uint)paddingFrames;
        WriteSilentFrames(framesAvailable);
    }

    private void RenderLoop(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                int timeoutMs = Math.Max((int)(LatencyMs * 2), 100);

                int waitResult = WaitForSingleObject(
                    _eventHandle!.DangerousGetHandle(),
                    timeoutMs);

                if (cancellationToken.IsCancellationRequested) break;

                switch (waitResult)
                {
                    case 0:
                        OnBufferEvent();
                        break;
                    case 0x102:
                        break;
                    default:
                        return;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            LogError?.Invoke($"Fatal error in render loop: {ex}");
        }
    }

    private void OnBufferEvent()
    {
        int hr = _audioClient!.GetCurrentPadding(out int paddingFrames);
        if (hr != 0) return;

        uint framesAvailable = _bufferFrameCount - (uint)paddingFrames;
        if (framesAvailable == 0) return;

        WriteAudioFrames(framesAvailable);
    }

    private void WriteAudioFrames(uint frameCount)
    {
        if (frameCount == 0) return;

        IntPtr dataPtr;
        int hr = _renderClient!.GetBuffer(frameCount, out dataPtr);
        if (hr != 0) return;

        int byteCount = (int)frameCount * _frameSize;

        try
        {
            var ringBuf = RingBuffer;
            if (ringBuf != null && ringBuf.AvailableBytes >= byteCount)
            {
                byte[] tempBuffer = new byte[byteCount];
                int bytesRead = ringBuf.Read(tempBuffer);
                if (bytesRead > 0)
                {
                    Marshal.Copy(tempBuffer, 0, dataPtr, bytesRead);
                    uint framesWritten = (uint)(bytesRead / _frameSize);
                    int flags = bytesRead < byteCount ? AudClnt.BUFFERFLAGS_DATA_DISCONTINUITY : 0;
                    _renderClient!.ReleaseBuffer(framesWritten, flags);
                }
                else
                {
                    _renderClient!.ReleaseBuffer(frameCount, AudClnt.BUFFERFLAGS_SILENT);
                }
            }
            else
            {
                _renderClient!.ReleaseBuffer(frameCount, AudClnt.BUFFERFLAGS_SILENT);
            }
        }
        catch
        {
            try
            {
                _renderClient!.ReleaseBuffer(frameCount, AudClnt.BUFFERFLAGS_SILENT);
            }
            catch
            {
            }
        }
    }

    private void WriteSilentFrames(uint frameCount)
    {
        if (frameCount == 0) return;

        IntPtr dataPtr;
        int hr = _renderClient!.GetBuffer(frameCount, out dataPtr);
        if (hr != 0) return;

        _renderClient!.ReleaseBuffer(frameCount, AudClnt.BUFFERFLAGS_SILENT);
    }

    private void CleanupResources()
    {
        if (_isPlaying) Stop();

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        try { _renderTask?.Wait(TimeSpan.FromSeconds(5)); } catch { }
        _renderTask = null;

        if (_streamVolume != null)
        {
            try { Marshal.ReleaseComObject(_streamVolume); } catch { }
            _streamVolume = null;
        }

        if (_renderClient != null)
        {
            try { Marshal.ReleaseComObject(_renderClient); } catch { }
            _renderClient = null;
        }

        if (_audioClient != null)
        {
            try { Marshal.ReleaseComObject(_audioClient); } catch { }
            _audioClient = null;
        }

        if (_audioClientComPtr != IntPtr.Zero)
        {
            Marshal.Release(_audioClientComPtr);
            _audioClientComPtr = IntPtr.Zero;
        }

        RingBuffer?.Dispose();
        RingBuffer = null;

        _eventHandle?.Dispose();
        _eventHandle = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        CleanupResources();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    [LibraryImport("ole32.dll")]
    private static partial int CoCreateInstance(
        ref Guid rclsid,
        IntPtr pUnkOuter,
        uint dwClsCtx,
        ref Guid riid,
        out IntPtr ppv);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial IntPtr CreateEvent(
        IntPtr lpEventAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool bManualReset,
        [MarshalAs(UnmanagedType.Bool)] bool bInitialState,
        IntPtr lpName);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial int WaitForSingleObject(IntPtr hHandle, int dwMilliseconds);

    [LibraryImport("ole32.dll")]
    private static partial void CoTaskMemFree(IntPtr ptr);
}
