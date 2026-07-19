namespace WorldNet;

[ScratchLayout]
internal unsafe partial struct SynthesisScratch
{
    public double* ImpulseResponse;
    public MinimumPhaseAnalysis MinimumPhase;
    public InverseRealFft InverseRealFft;
    public ForwardRealFft ForwardRealFft;
    public double* PulseLocations;
    public int* PulseLocationsIndex;
    public double* PulseLocationsTimeShift;
    public double* InterpolatedVuv;
    public double* DcRemover;
    public double* TimeAxis;
    public double* CoarseTimeAxis;
    public double* CoarseF0;
    public double* CoarseVuv;
    public double* InterpolatedF0;
    public Interp1Scratch Interpolation;
    public double* TotalPhase;
    public double* WrapPhase;
    public double* WrapPhaseAbs;
    public double* AperiodicResponse;
    public double* PeriodicResponse;
    public double* SpectralEnvelope;
    public double* AperiodicRatio;

    public static void Layout<TAllocator>(ref TAllocator allocator, int fftSize, int yLength,
        int f0Length, ref SynthesisScratch scratch)
        where TAllocator : struct, IScratchAllocator
    {
        scratch.ImpulseResponse = (double*)allocator.Allocate(fftSize, sizeof(double));
        MinimumPhaseAnalysis.Layout(ref allocator, fftSize, ref scratch.MinimumPhase);
        InverseRealFft.Layout(ref allocator, fftSize, ref scratch.InverseRealFft);
        ForwardRealFft.Layout(ref allocator, fftSize, ref scratch.ForwardRealFft);
        scratch.PulseLocations = (double*)allocator.Allocate(yLength, sizeof(double));
        scratch.PulseLocationsIndex = (int*)allocator.Allocate(yLength, sizeof(int));
        scratch.PulseLocationsTimeShift = (double*)allocator.Allocate(yLength, sizeof(double));
        scratch.InterpolatedVuv = (double*)allocator.Allocate(yLength, sizeof(double));
        scratch.DcRemover = (double*)allocator.Allocate(fftSize, sizeof(double));
        scratch.TimeAxis = (double*)allocator.Allocate(yLength, sizeof(double));
        scratch.CoarseTimeAxis = (double*)allocator.Allocate(f0Length + 1, sizeof(double));
        scratch.CoarseF0 = (double*)allocator.Allocate(f0Length + 1, sizeof(double));
        scratch.CoarseVuv = (double*)allocator.Allocate(f0Length + 1, sizeof(double));
        scratch.InterpolatedF0 = (double*)allocator.Allocate(yLength, sizeof(double));
        Interp1Scratch.Layout(ref allocator, f0Length + 1, yLength, ref scratch.Interpolation);
        scratch.TotalPhase = (double*)allocator.Allocate(yLength, sizeof(double));
        scratch.WrapPhase = (double*)allocator.Allocate(yLength, sizeof(double));
        scratch.WrapPhaseAbs = (double*)allocator.Allocate(yLength - 1, sizeof(double));
        scratch.AperiodicResponse = (double*)allocator.Allocate(fftSize, sizeof(double));
        scratch.PeriodicResponse = (double*)allocator.Allocate(fftSize, sizeof(double));
        scratch.SpectralEnvelope = (double*)allocator.Allocate(fftSize, sizeof(double));
        scratch.AperiodicRatio = (double*)allocator.Allocate(fftSize, sizeof(double));
    }

    public static SynthesisScratch Bind(WorldArena arena, int fftSize, int yLength, int f0Length)
    {
        ArenaAllocator allocator = new(arena);
        SynthesisScratch scratch = default;
        Layout(ref allocator, fftSize, yLength, f0Length, ref scratch);
        scratch.MinimumPhase.Initialize(fftSize);
        scratch.InverseRealFft.Initialize(fftSize);
        scratch.ForwardRealFft.Initialize(fftSize);
        return scratch;
    }
}
