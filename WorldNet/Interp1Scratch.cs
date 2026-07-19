namespace WorldNet;

[ScratchLayout]
internal unsafe partial struct Interp1Scratch
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


}
