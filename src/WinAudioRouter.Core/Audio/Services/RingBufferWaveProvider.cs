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
            int remaining = count - bytesRead;
            if (bytesRead > 0)
            {
                int pos = 0;
                while (pos < remaining)
                {
                    int copyLen = Math.Min(bytesRead, remaining - pos);
                    Array.Copy(buffer, offset + pos % bytesRead, buffer, offset + bytesRead + pos, copyLen);
                    pos += copyLen;
                }
            }
            else
            {
                Array.Clear(buffer, offset + bytesRead, remaining);
            }
        }

        return count;
    }
}
