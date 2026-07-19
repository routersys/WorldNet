namespace WorldNet;

internal unsafe struct LinearSmoothingScratch
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

    public static nuint GetRequiredArenaBytes(int fftSize, int boundary)
    {
        MeasuringAllocator allocator = default;
        LinearSmoothingScratch scratch = default;
        Layout(ref allocator, fftSize, boundary, ref scratch);
        return allocator.Total;
    }

    public static LinearSmoothingScratch Bind(WorldArena arena, int fftSize, int boundary)
    {
        ArenaAllocator allocator = new(arena);
        LinearSmoothingScratch scratch = default;
        Layout(ref allocator, fftSize, boundary, ref scratch);
        return scratch;
    }
}
