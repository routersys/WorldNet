namespace WorldNet;

internal unsafe struct StoneMaskScratch
{
    public const int MaximumHarmonics = 6;

    public double* BaseTime;
    public int* IndexRaw;
    public int* Index;
    public double* MainWindow;
    public double* DiffWindow;
    public FftComplex* MainSpectrum;
    public FftComplex* DiffSpectrum;
    public double* PowerSpectrum;
    public double* NumeratorI;
    public double* AmplitudeList;
    public double* InstantaneousFrequencyList;
    public ForwardRealFft ForwardRealFft;

    public static void Layout<TAllocator>(ref TAllocator allocator, int baseTimeLength,
        int fftSize, ref StoneMaskScratch scratch)
        where TAllocator : struct, IScratchAllocator
    {
        scratch.BaseTime = (double*)allocator.Allocate(baseTimeLength, sizeof(double));
        scratch.IndexRaw = (int*)allocator.Allocate(baseTimeLength, sizeof(int));
        scratch.Index = (int*)allocator.Allocate(baseTimeLength, sizeof(int));
        scratch.MainWindow = (double*)allocator.Allocate(baseTimeLength, sizeof(double));
        scratch.DiffWindow = (double*)allocator.Allocate(baseTimeLength, sizeof(double));
        scratch.MainSpectrum = (FftComplex*)allocator.Allocate(fftSize, (nuint)sizeof(FftComplex));
        scratch.DiffSpectrum = (FftComplex*)allocator.Allocate(fftSize, (nuint)sizeof(FftComplex));
        scratch.PowerSpectrum = (double*)allocator.Allocate((fftSize / 2) + 1, sizeof(double));
        scratch.NumeratorI = (double*)allocator.Allocate((fftSize / 2) + 1, sizeof(double));
        scratch.AmplitudeList = (double*)allocator.Allocate(MaximumHarmonics, sizeof(double));
        scratch.InstantaneousFrequencyList =
            (double*)allocator.Allocate(MaximumHarmonics, sizeof(double));
        ForwardRealFft.Layout(ref allocator, fftSize, ref scratch.ForwardRealFft);
    }

    public static nuint GetRequiredArenaBytes(int baseTimeLength, int fftSize)
    {
        MeasuringAllocator allocator = default;
        StoneMaskScratch scratch = default;
        Layout(ref allocator, baseTimeLength, fftSize, ref scratch);
        return allocator.Total;
    }

    public static StoneMaskScratch Bind(WorldArena arena, int baseTimeLength, int fftSize)
    {
        ArenaAllocator allocator = new(arena);
        StoneMaskScratch scratch = default;
        Layout(ref allocator, baseTimeLength, fftSize, ref scratch);
        scratch.ForwardRealFft.Initialize(fftSize);
        return scratch;
    }
}
