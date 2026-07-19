namespace WorldNet;

internal unsafe struct DecimateScratch
{
    public double* Tmp1;
    public double* Tmp2;

    public static void Layout<TAllocator>(ref TAllocator allocator, int xLength,
        ref DecimateScratch scratch)
        where TAllocator : struct, IScratchAllocator
    {
        int padded = xLength + (MatlabFunctions.DecimateFactorLength * 2);
        scratch.Tmp1 = (double*)allocator.Allocate(padded, sizeof(double));
        scratch.Tmp2 = (double*)allocator.Allocate(padded, sizeof(double));
    }

    public static nuint GetRequiredArenaBytes(int xLength)
    {
        MeasuringAllocator allocator = default;
        DecimateScratch scratch = default;
        Layout(ref allocator, xLength, ref scratch);
        return allocator.Total;
    }

    public static DecimateScratch Bind(WorldArena arena, int xLength)
    {
        ArenaAllocator allocator = new(arena);
        DecimateScratch scratch = default;
        Layout(ref allocator, xLength, ref scratch);
        return scratch;
    }
}
