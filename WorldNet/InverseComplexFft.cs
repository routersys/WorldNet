namespace WorldNet;

internal unsafe struct InverseComplexFft
{
    public int FftSize;
    public FftComplex* Input;
    public FftComplex* Output;
    public FftPlan InverseFft;

    public static void Layout<TAllocator>(ref TAllocator allocator, int fftSize,
        ref InverseComplexFft fft)
        where TAllocator : struct, IScratchAllocator
    {
        fft.Input = (FftComplex*)allocator.Allocate(fftSize, (nuint)sizeof(FftComplex));
        fft.Output = (FftComplex*)allocator.Allocate(fftSize, (nuint)sizeof(FftComplex));
        FftPlan.Layout(ref allocator, fftSize, FftPlanKind.ComplexToComplex, ref fft.InverseFft);
    }

    public static nuint GetRequiredArenaBytes(int fftSize)
    {
        MeasuringAllocator allocator = default;
        InverseComplexFft fft = default;
        Layout(ref allocator, fftSize, ref fft);
        return allocator.Total;
    }

    public static InverseComplexFft Bind(WorldArena arena, int fftSize)
    {
        ArenaAllocator allocator = new(arena);
        InverseComplexFft fft = default;
        Layout(ref allocator, fftSize, ref fft);
        fft.Initialize(fftSize);
        return fft;
    }

    public void Initialize(int fftSize)
    {
        FftSize = fftSize;
        InverseFft.InitializeComplexToComplex(fftSize, Input, Output, FftDirection.Backward);
    }
}
