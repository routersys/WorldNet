namespace WorldNet;

[ScratchLayout]
internal unsafe partial struct DecimateScratch
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
}
