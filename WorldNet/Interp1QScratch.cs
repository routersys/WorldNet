namespace WorldNet;

[ScratchLayout]
internal unsafe partial struct Interp1QScratch
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
}
