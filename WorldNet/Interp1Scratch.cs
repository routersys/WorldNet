namespace WorldNet;

internal unsafe struct Interp1Scratch
{
    public double* H;
    public int* K;

    public static void Layout<TAllocator>(ref TAllocator allocator, int xLength, int xiLength,
        ref Interp1Scratch scratch)
        where TAllocator : struct, IScratchAllocator
    {
        scratch.H = (double*)allocator.Allocate(xLength - 1, sizeof(double));
        scratch.K = (int*)allocator.Allocate(xiLength, sizeof(int));
    }

    public static nuint GetRequiredArenaBytes(int xLength, int xiLength)
    {
        MeasuringAllocator allocator = default;
        Interp1Scratch scratch = default;
        Layout(ref allocator, xLength, xiLength, ref scratch);
        return allocator.Total;
    }

    public static Interp1Scratch Bind(WorldArena arena, int xLength, int xiLength)
    {
        ArenaAllocator allocator = new(arena);
        Interp1Scratch scratch = default;
        Layout(ref allocator, xLength, xiLength, ref scratch);
        return scratch;
    }
}
