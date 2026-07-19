namespace WorldNet;

internal unsafe struct DioScratch
{
    public FftComplex* YSpectrum;
    public double* SpectrumY;
    public FftComplex* FilterSpectrum;
    public FftPlan SpectrumPlan;
    public DecimateScratch Decimate;
    public double** F0Candidates;
    public double* F0CandidatesStorage;
    public double** F0Scores;
    public double* F0ScoresStorage;
    public double* BestF0Contour;
    public double* F0Candidate;
    public double* F0Score;
    public double* FilteredSignal;
    public double* LowPassFilter;
    public FftComplex* LowPassFilterSpectrum;
    public FftPlan FilterForwardPlan;
    public FftPlan FilterInversePlan;
    public ZeroCrossings ZeroCrossings;
    public double** InterpolatedF0Set;
    public double* InterpolatedF0SetStorage;
    public Interp1Scratch Interpolation;
    public double* F0Tmp1;
    public double* F0Tmp2;
    public int* PositiveIndex;
    public int* NegativeIndex;
    public double* F0Base;

    public static void Layout<TAllocator>(ref TAllocator allocator, int numberOfBands,
        int xLength, int yLength, int f0Length, int fftSize, ref DioScratch scratch)
        where TAllocator : struct, IScratchAllocator
    {
        scratch.YSpectrum = (FftComplex*)allocator.Allocate(fftSize, (nuint)sizeof(FftComplex));
        scratch.SpectrumY = (double*)allocator.Allocate(fftSize, sizeof(double));
        scratch.FilterSpectrum = (FftComplex*)allocator.Allocate(fftSize, (nuint)sizeof(FftComplex));
        FftPlan.Layout(ref allocator, fftSize, FftPlanKind.RealToComplex, ref scratch.SpectrumPlan);
        DecimateScratch.Layout(ref allocator, xLength, ref scratch.Decimate);
        scratch.F0Candidates = (double**)allocator.Allocate(numberOfBands, (nuint)sizeof(double*));
        scratch.F0CandidatesStorage = (double*)allocator.Allocate(numberOfBands * f0Length, sizeof(double));
        scratch.F0Scores = (double**)allocator.Allocate(numberOfBands, (nuint)sizeof(double*));
        scratch.F0ScoresStorage = (double*)allocator.Allocate(numberOfBands * f0Length, sizeof(double));
        scratch.BestF0Contour = (double*)allocator.Allocate(f0Length, sizeof(double));
        scratch.F0Candidate = (double*)allocator.Allocate(f0Length, sizeof(double));
        scratch.F0Score = (double*)allocator.Allocate(f0Length, sizeof(double));
        scratch.FilteredSignal = (double*)allocator.Allocate(fftSize, sizeof(double));
        scratch.LowPassFilter = (double*)allocator.Allocate(fftSize, sizeof(double));
        scratch.LowPassFilterSpectrum = (FftComplex*)allocator.Allocate(fftSize, (nuint)sizeof(FftComplex));
        FftPlan.Layout(ref allocator, fftSize, FftPlanKind.RealToComplex, ref scratch.FilterForwardPlan);
        FftPlan.Layout(ref allocator, fftSize, FftPlanKind.ComplexToReal, ref scratch.FilterInversePlan);
        ZeroCrossings.Layout(ref allocator, yLength, ref scratch.ZeroCrossings);
        scratch.InterpolatedF0Set = (double**)allocator.Allocate(4, (nuint)sizeof(double*));
        scratch.InterpolatedF0SetStorage = (double*)allocator.Allocate(4 * f0Length, sizeof(double));
        Interp1Scratch.Layout(ref allocator, yLength, f0Length, ref scratch.Interpolation);
        scratch.F0Tmp1 = (double*)allocator.Allocate(f0Length, sizeof(double));
        scratch.F0Tmp2 = (double*)allocator.Allocate(f0Length, sizeof(double));
        scratch.PositiveIndex = (int*)allocator.Allocate(f0Length, sizeof(int));
        scratch.NegativeIndex = (int*)allocator.Allocate(f0Length, sizeof(int));
        scratch.F0Base = (double*)allocator.Allocate(f0Length, sizeof(double));
    }

    public static nuint GetRequiredArenaBytes(int numberOfBands, int xLength, int yLength,
        int f0Length, int fftSize)
    {
        MeasuringAllocator allocator = default;
        DioScratch scratch = default;
        Layout(ref allocator, numberOfBands, xLength, yLength, f0Length, fftSize, ref scratch);
        return allocator.Total;
    }

    public static DioScratch Bind(WorldArena arena, int numberOfBands, int xLength, int yLength,
        int f0Length, int fftSize)
    {
        ArenaAllocator allocator = new(arena);
        DioScratch scratch = default;
        Layout(ref allocator, numberOfBands, xLength, yLength, f0Length, fftSize, ref scratch);
        scratch.Initialize(numberOfBands, f0Length, fftSize);
        return scratch;
    }

    public void Initialize(int numberOfBands, int f0Length, int fftSize)
    {
        for (int i = 0; i < numberOfBands; ++i)
        {
            F0Candidates[i] = F0CandidatesStorage + (i * f0Length);
            F0Scores[i] = F0ScoresStorage + (i * f0Length);
        }
        for (int i = 0; i < 4; ++i)
        {
            InterpolatedF0Set[i] = InterpolatedF0SetStorage + (i * f0Length);
        }
        SpectrumPlan.InitializeRealToComplex(fftSize, SpectrumY, YSpectrum);
        FilterForwardPlan.InitializeRealToComplex(fftSize, LowPassFilter, LowPassFilterSpectrum);
        FilterInversePlan.InitializeComplexToReal(fftSize, LowPassFilterSpectrum, FilteredSignal);
    }
}
