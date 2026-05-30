using NAudio.Wave;
using WinAudioRouter.Native.Wrappers;

namespace WinAudioRouter.Core.Audio.Services;

public sealed class RingBufferWaveProvider : IWaveProvider
{
    private readonly LockFreeRingBuffer _ringBuffer;
    private readonly WaveFormat _waveFormat;

    public RingBufferWaveProvider(LockFreeRingBuffer ringBuffer, WaveFormat waveFormat)
    {
        _ringBuffer = ringBuffer;
        _waveFormat = waveFormat;
    }

    public WaveFormat WaveFormat => _waveFormat;

    public int Read(byte[] buffer, int offset, int count)
    {
        int bytesRead = _ringBuffer.Read(new Span<byte>(buffer, offset, count));

        if (bytesRead < count)
        {
            Array.Clear(buffer, offset + bytesRead, count - bytesRead);
        }

        return count;
    }
}
