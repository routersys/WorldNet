namespace WorldNet;

internal unsafe struct InverseRealFft
{
    public int FftSize;
    public double* Waveform;
    public FftComplex* Spectrum;
    public FftPlan InverseFft;

    public static void Layout<TAllocator>(ref TAllocator allocator, int fftSize,
        ref InverseRealFft fft)
        where TAllocator : struct, IScratchAllocator
    {
        fft.Waveform = (double*)allocator.Allocate(fftSize, sizeof(double));
        fft.Spectrum = (FftComplex*)allocator.Allocate(fftSize, (nuint)sizeof(FftComplex));
        FftPlan.Layout(ref allocator, fftSize, FftPlanKind.ComplexToReal, ref fft.InverseFft);
    }

    public static nuint GetRequiredArenaBytes(int fftSize)
    {
        MeasuringAllocator allocator = default;
        InverseRealFft fft = default;
        Layout(ref allocator, fftSize, ref fft);
        return allocator.Total;
    }

    public static InverseRealFft Bind(WorldArena arena, int fftSize)
    {
        ArenaAllocator allocator = new(arena);
        InverseRealFft fft = default;
        Layout(ref allocator, fftSize, ref fft);
        fft.FftSize = fftSize;
        fft.InverseFft.InitializeComplexToReal(fftSize, fft.Spectrum, fft.Waveform);
        return fft;
    }
}
