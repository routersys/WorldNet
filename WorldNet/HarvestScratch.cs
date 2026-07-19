namespace WorldNet;

[ScratchLayout]
internal unsafe partial struct HarvestScratch
{
    public double* Y;
    public FftComplex* YSpectrum;
    public FftPlan WaveformPlan;
    public double* NewX;
    public double* NewY;
    public DecimateScratch WaveformDecimate;
    public double** RawF0Candidates;
    public double* RawF0CandidatesStorage;
    public double** F0Candidates;
    public double* F0CandidatesStorage;
    public double** F0CandidatesScore;
    public double* F0CandidatesScoreStorage;
    public double* BestF0Contour;
    public double* FilteredSignal;
    public double* BandPassFilter;
    public FftComplex* BandPassFilterSpectrum;
    public FftPlan FilterForwardPlan;
    public FftPlan FilterInversePlan;
    public ZeroCrossings ZeroCrossings;
    public double** InterpolatedF0Set;
    public double* InterpolatedF0SetStorage;
    public Interp1Scratch Interpolation;
    public int* Vuv;
    public int* St;
    public int* Ed;

    public static void Layout<TAllocator>(ref TAllocator allocator, int numberOfChannels,
        int newXLength, int yLength, int f0Length, int maxCandidates, int fftSize,
        ref HarvestScratch scratch)
        where TAllocator : struct, IScratchAllocator
    {
        scratch.Y = (double*)allocator.Allocate(fftSize, sizeof(double));
        scratch.YSpectrum = (FftComplex*)allocator.Allocate(fftSize, (nuint)sizeof(FftComplex));
        FftPlan.Layout(ref allocator, fftSize, FftPlanKind.RealToComplex, ref scratch.WaveformPlan);
        scratch.NewX = (double*)allocator.Allocate(newXLength, sizeof(double));
        scratch.NewY = (double*)allocator.Allocate(newXLength, sizeof(double));
        DecimateScratch.Layout(ref allocator, newXLength, ref scratch.WaveformDecimate);
        scratch.RawF0Candidates = (double**)allocator.Allocate(numberOfChannels, (nuint)sizeof(double*));
        scratch.RawF0CandidatesStorage = (double*)allocator.Allocate(numberOfChannels * f0Length, sizeof(double));
        scratch.F0Candidates = (double**)allocator.Allocate(f0Length, (nuint)sizeof(double*));
        scratch.F0CandidatesStorage = (double*)allocator.Allocate(f0Length * maxCandidates, sizeof(double));
        scratch.F0CandidatesScore = (double**)allocator.Allocate(f0Length, (nuint)sizeof(double*));
        scratch.F0CandidatesScoreStorage = (double*)allocator.Allocate(f0Length * maxCandidates, sizeof(double));
        scratch.BestF0Contour = (double*)allocator.Allocate(f0Length, sizeof(double));
        scratch.FilteredSignal = (double*)allocator.Allocate(fftSize, sizeof(double));
        scratch.BandPassFilter = (double*)allocator.Allocate(fftSize, sizeof(double));
        scratch.BandPassFilterSpectrum = (FftComplex*)allocator.Allocate(fftSize, (nuint)sizeof(FftComplex));
        FftPlan.Layout(ref allocator, fftSize, FftPlanKind.RealToComplex, ref scratch.FilterForwardPlan);
        FftPlan.Layout(ref allocator, fftSize, FftPlanKind.ComplexToReal, ref scratch.FilterInversePlan);
        ZeroCrossings.Layout(ref allocator, yLength, ref scratch.ZeroCrossings);
        scratch.InterpolatedF0Set = (double**)allocator.Allocate(4, (nuint)sizeof(double*));
        scratch.InterpolatedF0SetStorage = (double*)allocator.Allocate(4 * f0Length, sizeof(double));
        Interp1Scratch.Layout(ref allocator, yLength, f0Length, ref scratch.Interpolation);
        scratch.Vuv = (int*)allocator.Allocate(numberOfChannels, sizeof(int));
        scratch.St = (int*)allocator.Allocate(numberOfChannels, sizeof(int));
        scratch.Ed = (int*)allocator.Allocate(numberOfChannels, sizeof(int));
    }

    public static HarvestScratch Bind(WorldArena arena, int numberOfChannels, int newXLength,
        int yLength, int f0Length, int maxCandidates, int fftSize)
    {
        ArenaAllocator allocator = new(arena);
        HarvestScratch scratch = default;
        Layout(ref allocator, numberOfChannels, newXLength, yLength, f0Length, maxCandidates,
            fftSize, ref scratch);
        scratch.Initialize(numberOfChannels, f0Length, maxCandidates, fftSize);
        return scratch;
    }

    public void Initialize(int numberOfChannels, int f0Length, int maxCandidates, int fftSize)
    {
        for (int i = 0; i < numberOfChannels; ++i)
        {
            RawF0Candidates[i] = RawF0CandidatesStorage + (i * f0Length);
        }
        for (int i = 0; i < f0Length; ++i)
        {
            F0Candidates[i] = F0CandidatesStorage + (i * maxCandidates);
            F0CandidatesScore[i] = F0CandidatesScoreStorage + (i * maxCandidates);
        }
        for (int i = 0; i < 4; ++i)
        {
            InterpolatedF0Set[i] = InterpolatedF0SetStorage + (i * f0Length);
        }
        WaveformPlan.InitializeRealToComplex(fftSize, Y, YSpectrum);
        FilterForwardPlan.InitializeRealToComplex(fftSize, BandPassFilter, BandPassFilterSpectrum);
        FilterInversePlan.InitializeComplexToReal(fftSize, BandPassFilterSpectrum, FilteredSignal);
    }
}
