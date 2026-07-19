using System.Runtime.InteropServices;

namespace WorldNet.Tests;

public unsafe class WorldArenaTests
{
    [Fact]
    public void AllocateDoubleReturnsRequestedLength()
    {
        using WorldArena arena = new(1 << 16);

        Span<double> values = arena.AllocateDouble(129);

        Assert.Equal(129, values.Length);
    }

    [Fact]
    public void AllocationsAreAlignedToCacheLine()
    {
        using WorldArena arena = new(1 << 16);

        for (int count = 1; count <= 33; ++count)
        {
            Span<double> values = arena.AllocateDouble(count);
            fixed (double* pointer = values)
            {
                Assert.Equal((nuint)0, (nuint)pointer & 63);
            }
        }
    }

    [Fact]
    public void SeparateAllocationsDoNotOverlap()
    {
        using WorldArena arena = new(1 << 16);

        Span<double> first = arena.AllocateDouble(8);
        Span<double> second = arena.AllocateDouble(8);

        first.Fill(1.0);
        second.Fill(2.0);

        Assert.All(first.ToArray(), value => Assert.Equal(1.0, value));
        Assert.All(second.ToArray(), value => Assert.Equal(2.0, value));
    }

    [Fact]
    public void ScopeRestoresUsedBytes()
    {
        using WorldArena arena = new(1 << 16);
        arena.AllocateDouble(16);
        nuint used = arena.Used;

        using (arena.BeginScope())
        {
            arena.AllocateDouble(256);
            Assert.True(arena.Used > used);
        }

        Assert.Equal(used, arena.Used);
    }

    [Fact]
    public void NestedScopesRestoreIndependently()
    {
        using WorldArena arena = new(1 << 16);

        using (arena.BeginScope())
        {
            arena.AllocateDouble(8);
            nuint outer = arena.Used;

            using (arena.BeginScope())
            {
                arena.AllocateDouble(8);
            }

            Assert.Equal(outer, arena.Used);
        }

        Assert.Equal((nuint)0, arena.Used);
    }

    [Fact]
    public void ExceedingCapacityThrows()
    {
        using WorldArena arena = new(128);

        Assert.Throws<InvalidOperationException>(() => arena.AllocateDouble(1024));
    }

    [Fact]
    public void AllocationConsumingExactCapacitySucceeds()
    {
        using WorldArena arena = new(64);

        Span<double> values = arena.AllocateDouble(8);

        Assert.Equal(8, values.Length);
        Assert.Equal((nuint)64, arena.Used);
    }

    [Fact]
    public void NegativeCountThrows()
    {
        using WorldArena arena = new(64);

        Assert.Throws<ArgumentOutOfRangeException>(() => arena.AllocateDouble(-1));
    }

    [Fact]
    public void ZeroCountAllocationSucceeds()
    {
        using WorldArena arena = new(64);

        Span<double> values = arena.AllocateDouble(0);

        Assert.Equal(0, values.Length);
        Assert.Equal((nuint)0, arena.Used);
    }

    [Fact]
    public void EnsureCapacityGrowsEmptyArena()
    {
        using WorldArena arena = new();

        arena.EnsureCapacity(4096);

        Assert.True(arena.Capacity >= 4096);
        Assert.Equal(512, arena.AllocateDouble(512).Length);
    }

    [Fact]
    public void EnsureCapacityKeepsLargerCapacity()
    {
        using WorldArena arena = new(8192);

        arena.EnsureCapacity(1024);

        Assert.Equal((nuint)8192, arena.Capacity);
    }

    [Fact]
    public void EnsureCapacityThrowsWhileAllocationsOutstanding()
    {
        using WorldArena arena = new(128);
        arena.AllocateDouble(8);

        Assert.Throws<InvalidOperationException>(() => arena.EnsureCapacity(1 << 20));
    }

    [Fact]
    public void FromNativeMemoryRejectsMisalignedBuffer()
    {
        byte* buffer = (byte*)NativeMemory.AlignedAlloc(256, 64);
        try
        {
            Assert.Throws<ArgumentException>(() => WorldArena.FromNativeMemory(buffer + 8, 128));
        }
        finally
        {
            NativeMemory.AlignedFree(buffer);
        }
    }

    [Fact]
    public void FromNativeMemoryRejectsNullBuffer()
    {
        Assert.Throws<ArgumentNullException>(() => WorldArena.FromNativeMemory(null, 128));
    }

    [Fact]
    public void CallerSuppliedArenaServesAllocations()
    {
        byte* buffer = (byte*)NativeMemory.AlignedAlloc(1024, 64);
        try
        {
            using WorldArena arena = WorldArena.FromNativeMemory(buffer, 1024);

            Span<double> values = arena.AllocateDouble(64);
            values.Fill(3.0);

            Assert.Equal(3.0, values[63]);
            Assert.Equal((nuint)512, arena.Used);
        }
        finally
        {
            NativeMemory.AlignedFree(buffer);
        }
    }

    [Fact]
    public void CallerSuppliedArenaCannotGrow()
    {
        byte* buffer = (byte*)NativeMemory.AlignedAlloc(1024, 64);
        try
        {
            using WorldArena arena = WorldArena.FromNativeMemory(buffer, 1024);

            Assert.Throws<InvalidOperationException>(() => arena.EnsureCapacity(1 << 20));
        }
        finally
        {
            NativeMemory.AlignedFree(buffer);
        }
    }

    [Fact]
    public void DisposeIsIdempotent()
    {
        WorldArena arena = new(256);

        arena.Dispose();
        arena.Dispose();
    }

    [Fact]
    public void AllocationAfterDisposeThrows()
    {
        WorldArena arena = new(256);
        arena.Dispose();

        Assert.Throws<ObjectDisposedException>(() => arena.AllocateDouble(1));
    }

    [Fact]
    public void ResetReleasesEveryAllocation()
    {
        using WorldArena arena = new(1 << 16);
        arena.AllocateDouble(512);

        arena.Reset();

        Assert.Equal((nuint)0, arena.Used);
    }

    [Fact]
    public void AllocationCyclesDoNotAllocateManagedMemory()
    {
        using WorldArena arena = new(1 << 20);

        for (int i = 0; i < 64; ++i)
        {
            RunAllocationCycle(arena);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 4096; ++i)
        {
            RunAllocationCycle(arena);
        }
        long after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(0L, after - before);
    }

    private static void RunAllocationCycle(WorldArena arena)
    {
        using WorldArenaScope scope = arena.BeginScope();
        Span<double> samples = arena.AllocateDouble(256);
        Span<int> indices = arena.AllocateInt(128);
        samples[255] = 1.0;
        indices[127] = 2;
    }
}
