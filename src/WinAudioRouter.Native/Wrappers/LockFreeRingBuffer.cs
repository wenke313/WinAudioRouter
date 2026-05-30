using System.Runtime.InteropServices;

namespace WinAudioRouter.Native.Wrappers;

public sealed class LockFreeRingBuffer : IDisposable
{
    private readonly byte[] _buffer;
    private readonly int _capacity;
    private readonly int _frameSize;
    private int _writePos;
    private int _readPos;
    private volatile bool _disposed;

    public int AvailableBytes
    {
        get
        {
            int writePos = Volatile.Read(ref _writePos);
            int readPos = Volatile.Read(ref _readPos);
            int available = writePos - readPos;
            if (available < 0) available += _capacity;
            return available;
        }
    }

    public int Capacity => _capacity - 1;

    public LockFreeRingBuffer(int capacityBytes, int frameSize)
    {
        _capacity = RoundUpToPowerOf2(capacityBytes + 1);
        _buffer = GC.AllocateArray<byte>(_capacity, pinned: true);
        _frameSize = frameSize;
        _writePos = 0;
        _readPos = 0;
    }

    public int Write(ReadOnlySpan<byte> source)
    {
        if (_disposed || source.IsEmpty) return 0;

        int writePos = Volatile.Read(ref _writePos);
        int readPos = Volatile.Read(ref _readPos);
        int available = writePos - readPos;
        if (available < 0) available += _capacity;

        int freeSpace = _capacity - 1 - available;
        int bytesToWrite = Math.Min(source.Length, freeSpace);
        if (bytesToWrite == 0) return 0;

        int writeIdx = writePos & (_capacity - 1);
        int firstChunk = Math.Min(bytesToWrite, _capacity - writeIdx);

        if (firstChunk > 0)
        {
            source.Slice(0, firstChunk).CopyTo(_buffer.AsSpan(writeIdx));
        }

        if (bytesToWrite > firstChunk)
        {
            source.Slice(firstChunk, bytesToWrite - firstChunk).CopyTo(_buffer.AsSpan());
        }

        Volatile.Write(ref _writePos, (writePos + bytesToWrite) % (_capacity * 2));

        return bytesToWrite;
    }

    public int Read(Span<byte> destination)
    {
        if (_disposed) return 0;

        int writePos = Volatile.Read(ref _writePos);
        int readPos = Volatile.Read(ref _readPos);
        int available = writePos - readPos;
        if (available < 0) available += _capacity * 2;
        if (available > _capacity - 1) available = _capacity - 1;

        int bytesToRead = Math.Min(destination.Length, available);
        if (bytesToRead == 0) return 0;

        int readIdx = readPos & (_capacity - 1);
        int firstChunk = Math.Min(bytesToRead, _capacity - readIdx);

        if (firstChunk > 0)
        {
            _buffer.AsSpan(readIdx, firstChunk).CopyTo(destination.Slice(0, firstChunk));
        }

        if (bytesToRead > firstChunk)
        {
            _buffer.AsSpan(0, bytesToRead - firstChunk).CopyTo(destination.Slice(firstChunk));
        }

        Volatile.Write(ref _readPos, (readPos + bytesToRead) % (_capacity * 2));

        return bytesToRead;
    }

    public void Reset()
    {
        Volatile.Write(ref _writePos, 0);
        Volatile.Write(ref _readPos, 0);
    }

    private static int RoundUpToPowerOf2(int value)
    {
        value--;
        value |= value >> 1;
        value |= value >> 2;
        value |= value >> 4;
        value |= value >> 8;
        value |= value >> 16;
        return value + 1;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}
