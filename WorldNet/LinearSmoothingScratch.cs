namespace WorldNet;

[ScratchLayout]
internal unsafe partial struct LinearSmoothingScratch
{
    public double* MirroringSpectrum;
    public double* MirroringSegment;
    public double* FrequencyAxis;
    public double* LowLevels;
    public double* HighLevels;
    public Interp1QScratch Interpolation;

    public static void Layout<TAllocator>(ref TAllocator allocator, int fftSize, int boundary,
        ref LinearSmoothingScratch scratch)
        where TAllocator : struct, IScratchAllocator
    {
        int mirroringLength = (fftSize / 2) + (boundary * 2) + 1;
        int spectrumLength = (fftSize / 2) + 1;
        scratch.MirroringSpectrum = (double*)allocator.Allocate(mirroringLength, sizeof(double));
        scratch.MirroringSegment = (double*)allocator.Allocate(mirroringLength, sizeof(double));
        scratch.FrequencyAxis = (double*)allocator.Allocate(spectrumLength, sizeof(double));
        scratch.LowLevels = (double*)allocator.Allocate(spectrumLength, sizeof(double));
        scratch.HighLevels = (double*)allocator.Allocate(spectrumLength, sizeof(double));
        Interp1QScratch.Layout(
            ref allocator, mirroringLength, spectrumLength, ref scratch.Interpolation);
    }
}
