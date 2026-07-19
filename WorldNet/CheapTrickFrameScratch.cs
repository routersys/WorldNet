namespace WorldNet;

[ScratchLayout]
internal unsafe partial struct CheapTrickFrameScratch
{
    public int* BaseIndex;
    public int* SafeIndex;
    public double* Window;
    public DcCorrectionScratch DcCorrection;
    public LinearSmoothingScratch LinearSmoothing;

    public static void Layout<TAllocator>(ref TAllocator allocator, int halfWindowLength,
        int upperLimit, int fftSize, int boundary, ref CheapTrickFrameScratch scratch)
        where TAllocator : struct, IScratchAllocator
    {
        int windowLength = (halfWindowLength * 2) + 1;
        scratch.BaseIndex = (int*)allocator.Allocate(windowLength, sizeof(int));
        scratch.SafeIndex = (int*)allocator.Allocate(windowLength, sizeof(int));
        scratch.Window = (double*)allocator.Allocate(windowLength, sizeof(double));
        DcCorrectionScratch.Layout(ref allocator, upperLimit, ref scratch.DcCorrection);
        LinearSmoothingScratch.Layout(ref allocator, fftSize, boundary, ref scratch.LinearSmoothing);
    }
}
