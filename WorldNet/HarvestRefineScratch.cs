namespace WorldNet;

[ScratchLayout]
internal unsafe partial struct HarvestRefineScratch
{
    public const int MaximumHarmonics = 6;

    public double* BaseTime;
    public ForwardRealFft ForwardRealFft;
    public FftComplex* MainSpectrum;
    public FftComplex* DiffSpectrum;
    public int* BaseIndex;
    public double* MainWindow;
    public double* DiffWindow;
    public int* SafeIndex;
    public double* PowerSpectrum;
    public double* NumeratorI;
    public double* AmplitudeList;
    public double* InstantaneousFrequencyList;

    public static void Layout<TAllocator>(ref TAllocator allocator, int fftSize,
        int baseTimeLength, ref HarvestRefineScratch scratch)
        where TAllocator : struct, IScratchAllocator
    {
        scratch.BaseTime = (double*)allocator.Allocate(baseTimeLength, sizeof(double));
        ForwardRealFft.Layout(ref allocator, fftSize, ref scratch.ForwardRealFft);
        scratch.MainSpectrum = (FftComplex*)allocator.Allocate(fftSize, (nuint)sizeof(FftComplex));
        scratch.DiffSpectrum = (FftComplex*)allocator.Allocate(fftSize, (nuint)sizeof(FftComplex));
        scratch.BaseIndex = (int*)allocator.Allocate(baseTimeLength, sizeof(int));
        scratch.MainWindow = (double*)allocator.Allocate(baseTimeLength, sizeof(double));
        scratch.DiffWindow = (double*)allocator.Allocate(baseTimeLength, sizeof(double));
        scratch.SafeIndex = (int*)allocator.Allocate(baseTimeLength, sizeof(int));
        scratch.PowerSpectrum = (double*)allocator.Allocate((fftSize / 2) + 1, sizeof(double));
        scratch.NumeratorI = (double*)allocator.Allocate((fftSize / 2) + 1, sizeof(double));
        scratch.AmplitudeList = (double*)allocator.Allocate(MaximumHarmonics, sizeof(double));
        scratch.InstantaneousFrequencyList =
            (double*)allocator.Allocate(MaximumHarmonics, sizeof(double));
    }

    public static HarvestRefineScratch Bind(WorldArena arena, int fftSize, int baseTimeLength)
    {
        ArenaAllocator allocator = new(arena);
        HarvestRefineScratch scratch = default;
        Layout(ref allocator, fftSize, baseTimeLength, ref scratch);
        scratch.ForwardRealFft.Initialize(fftSize);
        return scratch;
    }
}
