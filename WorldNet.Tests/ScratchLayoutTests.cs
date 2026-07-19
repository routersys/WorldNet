using System.Runtime.InteropServices;

namespace WorldNet.Tests;

public unsafe class ScratchLayoutTests
{
    [Theory]
    [InlineData(2, 1)]
    [InlineData(100, 397)]
    [InlineData(4096, 8191)]
    public void Interp1MeasurementMatchesBinding(int xLength, int xiLength)
    {
        nuint required = Interp1Scratch.GetRequiredArenaBytes(xLength, xiLength);

        using WorldArena arena = new();
        Interp1Scratch.Bind(arena, xLength, xiLength);

        Assert.Equal(required, arena.Used);
    }

    [Theory]
    [InlineData(2, 1)]
    [InlineData(100, 397)]
    [InlineData(4096, 8191)]
    public void Interp1QMeasurementMatchesBinding(int xLength, int xiLength)
    {
        nuint required = Interp1QScratch.GetRequiredArenaBytes(xLength, xiLength);

        using WorldArena arena = new();
        Interp1QScratch.Bind(arena, xLength, xiLength);

        Assert.Equal(required, arena.Used);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(1000)]
    [InlineData(48000)]
    public void DecimateMeasurementMatchesBinding(int xLength)
    {
        nuint required = DecimateScratch.GetRequiredArenaBytes(xLength);

        using WorldArena arena = new();
        DecimateScratch.Bind(arena, xLength);

        Assert.Equal(required, arena.Used);
    }

    [Fact]
    public void ExactlySizedCallerBufferSatisfiesBinding()
    {
        const int xLength = 100;
        const int xiLength = 397;
        nuint required = Interp1Scratch.GetRequiredArenaBytes(xLength, xiLength);
        nuint bufferBytes = required + 64;

        byte* buffer = (byte*)NativeMemory.AlignedAlloc(bufferBytes, 64);
        try
        {
            using WorldArena arena = WorldArena.FromNativeMemory(buffer, bufferBytes);

            Interp1Scratch scratch = Interp1Scratch.Bind(arena, xLength, xiLength);

            Assert.Equal(required, arena.Used);
            Assert.True(scratch.H is not null);
            Assert.True(scratch.K is not null);
        }
        finally
        {
            NativeMemory.AlignedFree(buffer);
        }
    }

    [Fact]
    public void UndersizedCallerBufferIsRejected()
    {
        const int xLength = 100;
        const int xiLength = 397;
        nuint required = Interp1Scratch.GetRequiredArenaBytes(xLength, xiLength);
        nuint bufferBytes = required;

        byte* buffer = (byte*)NativeMemory.AlignedAlloc(bufferBytes, 64);
        try
        {
            using WorldArena arena = WorldArena.FromNativeMemory(buffer, bufferBytes);

            Assert.Throws<InvalidOperationException>(
                () => Interp1Scratch.Bind(arena, xLength, xiLength));
        }
        finally
        {
            NativeMemory.AlignedFree(buffer);
        }
    }

    [Fact]
    public void BindingDoesNotAllocateManagedMemory()
    {
        using WorldArena arena = new(1 << 20);

        for (int i = 0; i < 64; ++i)
        {
            RunBindCycle(arena);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 4096; ++i)
        {
            RunBindCycle(arena);
        }
        long after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(0L, after - before);
    }

    private static void RunBindCycle(WorldArena arena)
    {
        using WorldArenaScope scope = arena.BeginScope();
        Interp1Scratch.Bind(arena, 100, 397);
        Interp1QScratch.Bind(arena, 100, 397);
        DecimateScratch.Bind(arena, 1000);
    }
}
