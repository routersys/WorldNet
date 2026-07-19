namespace WorldNet;

internal unsafe struct Interp1QScratch
{
    public double* XiFraction;
    public double* DeltaY;
    public int* XiBase;

    public static void Layout<TAllocator>(ref TAllocator allocator, int xLength, int xiLength,
        ref Interp1QScratch scratch)
        where TAllocator : struct, IScratchAllocator
    {
        scratch.XiFraction = (double*)allocator.Allocate(xiLength, sizeof(double));
        scratch.DeltaY = (double*)allocator.Allocate(xLength, sizeof(double));
        scratch.XiBase = (int*)allocator.Allocate(xiLength, sizeof(int));
    }

    public static nuint GetRequiredArenaBytes(int xLength, int xiLength)
    {
        MeasuringAllocator allocator = default;
        Interp1QScratch scratch = default;
        Layout(ref allocator, xLength, xiLength, ref scratch);
        return allocator.Total;
    }

    public static Interp1QScratch Bind(WorldArena arena, int xLength, int xiLength)
    {
        ArenaAllocator allocator = new(arena);
        Interp1QScratch scratch = default;
        Layout(ref allocator, xLength, xiLength, ref scratch);
        return scratch;
    }
}
