namespace WorldNet;

[ScratchLayout]
internal unsafe partial struct DcCorrectionScratch
{
    public double* LowFrequencyReplica;
    public double* LowFrequencyAxis;
    public Interp1QScratch Interpolation;

    public static void Layout<TAllocator>(ref TAllocator allocator, int upperLimit,
        ref DcCorrectionScratch scratch)
        where TAllocator : struct, IScratchAllocator
    {
        scratch.LowFrequencyReplica = (double*)allocator.Allocate(upperLimit, sizeof(double));
        scratch.LowFrequencyAxis = (double*)allocator.Allocate(upperLimit, sizeof(double));
        Interp1QScratch.Layout(
            ref allocator, upperLimit + 1, upperLimit - 1, ref scratch.Interpolation);
    }
}
