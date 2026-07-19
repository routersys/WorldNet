namespace WorldNet;

internal unsafe struct ForwardRealFft
{
    public int FftSize;
    public double* Waveform;
    public FftComplex* Spectrum;
    public FftPlan ForwardFft;

    public static void Layout<TAllocator>(ref TAllocator allocator, int fftSize,
        ref ForwardRealFft fft)
        where TAllocator : struct, IScratchAllocator
    {
        fft.Waveform = (double*)allocator.Allocate(fftSize, sizeof(double));
        fft.Spectrum = (FftComplex*)allocator.Allocate(fftSize, (nuint)sizeof(FftComplex));
        FftPlan.Layout(ref allocator, fftSize, FftPlanKind.RealToComplex, ref fft.ForwardFft);
    }

    public static nuint GetRequiredArenaBytes(int fftSize)
    {
        MeasuringAllocator allocator = default;
        ForwardRealFft fft = default;
        Layout(ref allocator, fftSize, ref fft);
        return allocator.Total;
    }

    public static ForwardRealFft Bind(WorldArena arena, int fftSize)
    {
        ArenaAllocator allocator = new(arena);
        ForwardRealFft fft = default;
        Layout(ref allocator, fftSize, ref fft);
        fft.Initialize(fftSize);
        return fft;
    }

    public void Initialize(int fftSize)
    {
        FftSize = fftSize;
        ForwardFft.InitializeRealToComplex(fftSize, Waveform, Spectrum);
    }
}
