using System.Runtime.InteropServices;

namespace WorldNet;

public sealed unsafe class WorldArena : IDisposable
{
    private const int AlignmentBytes = 64;
    private const int HeaderBytes = 64;
    private const nuint MinimumChunkBytes = 1 << 16;

    private struct Chunk
    {
        public Chunk* Next;
        public nuint Capacity;
        public nuint Used;
    }

    private Chunk* _head;
    private Chunk* _current;
    private readonly bool _ownsChunks;
    private bool _disposed;

    public WorldArena()
        : this(0)
    {
    }

    public WorldArena(nuint initialCapacityInBytes)
    {
        _ownsChunks = true;
        if (initialCapacityInBytes != 0)
        {
            _head = AllocateChunk(initialCapacityInBytes);
            _current = _head;
        }
    }

    private WorldArena(Chunk* chunk)
    {
        _head = chunk;
        _current = chunk;
        _ownsChunks = false;
    }

    ~WorldArena()
    {
        ReleaseChunks();
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

        if (capacityInBytes <= HeaderBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(capacityInBytes),
                $"The buffer must exceed the {HeaderBytes} byte arena header.");
        }

        Chunk* chunk = (Chunk*)buffer;
        chunk->Next = null;
        chunk->Capacity = capacityInBytes - HeaderBytes;
        chunk->Used = 0;
        return new WorldArena(chunk);
    }

    public static nuint GetReservedBytes(int count, nuint elementSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        nuint requested = checked((nuint)count * elementSize);
        nuint reserved = (requested + (AlignmentBytes - 1)) & ~(nuint)(AlignmentBytes - 1);

        if (reserved < requested)
        {
            throw new OverflowException(
                "The requested allocation size cannot be aligned without overflowing.");
        }

        return reserved;
    }

    public nuint Capacity
    {
        get
        {
            nuint total = 0;
            for (Chunk* chunk = _head; chunk is not null; chunk = chunk->Next)
            {
                total += chunk->Capacity;
            }
            return total;
        }
    }

    public nuint Used
    {
        get
        {
            nuint total = 0;
            for (Chunk* chunk = _head; chunk is not null; chunk = chunk->Next)
            {
                total += chunk->Used;
            }
            return total;
        }
    }

    public void EnsureCapacity(nuint byteCount)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        nuint available = 0;
        for (Chunk* chunk = _current; chunk is not null; chunk = chunk->Next)
        {
            available += chunk->Capacity - chunk->Used;
        }

        if (available >= byteCount)
        {
            return;
        }

        if (!_ownsChunks)
        {
            throw new InvalidOperationException(
                "The arena does not own its buffer and cannot grow.");
        }

        AppendChunk(byteCount - available);
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
        return new WorldArenaScope(this, _current, _current is null ? 0 : _current->Used);
    }

    public void Reset()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        for (Chunk* chunk = _head; chunk is not null; chunk = chunk->Next)
        {
            chunk->Used = 0;
        }
        _current = _head;
    }

    public void Dispose()
    {
        ReleaseChunks();
        GC.SuppressFinalize(this);
    }

    internal void* AllocateRaw(int count, nuint elementSize)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        nuint reserved = GetReservedBytes(count, elementSize);

        if (_current is null || _current->Used + reserved > _current->Capacity)
        {
            MoveToChunkWithSpace(reserved);
        }

        void* result = (byte*)_current + HeaderBytes + _current->Used;
        _current->Used += reserved;
        return result;
    }

    internal void RestoreTo(void* markChunk, nuint used)
    {
        Chunk* chunk = (Chunk*)markChunk;
        if (chunk is null)
        {
            Reset();
            return;
        }

        for (Chunk* next = chunk->Next; next is not null; next = next->Next)
        {
            next->Used = 0;
        }

        chunk->Used = used;
        _current = chunk;
    }

    private static Chunk* AllocateChunk(nuint usableBytes)
    {
        nuint total = checked(usableBytes + HeaderBytes);
        Chunk* chunk = (Chunk*)NativeMemory.AlignedAlloc(total, AlignmentBytes);
        chunk->Next = null;
        chunk->Capacity = usableBytes;
        chunk->Used = 0;
        return chunk;
    }

    private void MoveToChunkWithSpace(nuint reserved)
    {
        Chunk* start = _current is null ? null : _current->Next;
        for (Chunk* chunk = start; chunk is not null; chunk = chunk->Next)
        {
            if (chunk->Capacity - chunk->Used >= reserved)
            {
                _current = chunk;
                return;
            }
        }

        if (!_ownsChunks)
        {
            throw new InvalidOperationException(
                $"The caller supplied arena cannot satisfy a request of {reserved} bytes.");
        }

        AppendChunk(reserved);
    }

    private void AppendChunk(nuint reserved)
    {
        nuint usable = reserved > MinimumChunkBytes ? reserved : MinimumChunkBytes;
        Chunk* chunk = AllocateChunk(usable);

        if (_head is null)
        {
            _head = chunk;
        }
        else
        {
            Chunk* tail = _head;
            while (tail->Next is not null)
            {
                tail = tail->Next;
            }
            tail->Next = chunk;
        }

        _current = chunk;
    }

    private void ReleaseChunks()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_ownsChunks)
        {
            Chunk* chunk = _head;
            while (chunk is not null)
            {
                Chunk* next = chunk->Next;
                NativeMemory.AlignedFree(chunk);
                chunk = next;
            }
        }

        _head = null;
        _current = null;
    }
}
