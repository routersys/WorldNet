using System.Runtime.InteropServices;

namespace WorldNet;

public sealed unsafe class WorldArena : IDisposable
{
    private const int AlignmentBytes = 64;

    private byte* _buffer;
    private nuint _capacity;
    private nuint _offset;
    private readonly bool _ownsBuffer;
    private bool _disposed;

    public WorldArena()
        : this(0)
    {
    }

    public WorldArena(nuint capacityInBytes)
    {
        _ownsBuffer = true;
        if (capacityInBytes != 0)
        {
            _buffer = (byte*)NativeMemory.AlignedAlloc(capacityInBytes, AlignmentBytes);
            _capacity = capacityInBytes;
        }
    }

    private WorldArena(byte* buffer, nuint capacityInBytes)
    {
        _buffer = buffer;
        _capacity = capacityInBytes;
        _ownsBuffer = false;
    }

    ~WorldArena()
    {
        ReleaseBuffer();
    }

    public static WorldArena FromNativeMemory(void* buffer, nuint capacityInBytes)
    {
        if (buffer is null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        if (((nuint)buffer & (AlignmentBytes - 1)) != 0)
        {
            throw new ArgumentException(
                $"The buffer must be aligned to {AlignmentBytes} bytes.", nameof(buffer));
        }

        return new WorldArena((byte*)buffer, capacityInBytes);
    }

    public nuint Capacity => _capacity;

    public nuint Used => _offset;

    public void EnsureCapacity(nuint byteCount)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (byteCount <= _capacity)
        {
            return;
        }

        if (_offset != 0)
        {
            throw new InvalidOperationException(
                "The arena cannot grow while allocations are outstanding.");
        }

        if (!_ownsBuffer)
        {
            throw new InvalidOperationException(
                "The arena does not own its buffer and cannot grow.");
        }

        _buffer = (byte*)NativeMemory.AlignedRealloc(_buffer, byteCount, AlignmentBytes);
        _capacity = byteCount;
    }

    public Span<double> AllocateDouble(int count)
    {
        return new Span<double>(AllocateRaw(count, sizeof(double)), count);
    }

    public Span<int> AllocateInt(int count)
    {
        return new Span<int>(AllocateRaw(count, sizeof(int)), count);
    }

    public WorldArenaScope BeginScope()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new WorldArenaScope(this, _offset);
    }

    public void Reset()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _offset = 0;
    }

    public void Dispose()
    {
        ReleaseBuffer();
        GC.SuppressFinalize(this);
    }

    internal void* AllocateRaw(int count, nuint elementSize)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        nuint requested = checked((nuint)count * elementSize);
        nuint reserved = (requested + (AlignmentBytes - 1)) & ~(nuint)(AlignmentBytes - 1);

        if (reserved < requested || reserved > _capacity - _offset)
        {
            throw new InvalidOperationException(
                $"The arena has {_capacity - _offset} bytes available but {reserved} bytes were requested.");
        }

        void* result = _buffer + _offset;
        _offset += reserved;
        return result;
    }

    internal void RestoreTo(nuint offset)
    {
        _offset = offset;
    }

    private void ReleaseBuffer()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_ownsBuffer)
        {
            NativeMemory.AlignedFree(_buffer);
        }

        _buffer = null;
        _capacity = 0;
        _offset = 0;
    }
}
