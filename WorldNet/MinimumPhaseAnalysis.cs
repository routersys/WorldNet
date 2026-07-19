namespace WorldNet;

[ScratchLayout]
internal unsafe partial struct MinimumPhaseAnalysis
{
    public int FftSize;
    public double* LogSpectrum;
    public FftComplex* MinimumPhaseSpectrum;
    public FftComplex* Cepstrum;
    public FftPlan InverseFft;
    public FftPlan ForwardFft;

    public static void Layout<TAllocator>(ref TAllocator allocator, int fftSize,
        ref MinimumPhaseAnalysis analysis)
        where TAllocator : struct, IScratchAllocator
    {
        analysis.LogSpectrum = (double*)allocator.Allocate(fftSize, sizeof(double));
        analysis.MinimumPhaseSpectrum =
            (FftComplex*)allocator.Allocate(fftSize, (nuint)sizeof(FftComplex));
        analysis.Cepstrum = (FftComplex*)allocator.Allocate(fftSize, (nuint)sizeof(FftComplex));
        FftPlan.Layout(ref allocator, fftSize, FftPlanKind.RealToComplex, ref analysis.InverseFft);
        FftPlan.Layout(
            ref allocator, fftSize, FftPlanKind.ComplexToComplex, ref analysis.ForwardFft);
    }

    public static MinimumPhaseAnalysis Bind(WorldArena arena, int fftSize)
    {
        ArenaAllocator allocator = new(arena);
        MinimumPhaseAnalysis analysis = default;
        Layout(ref allocator, fftSize, ref analysis);
        analysis.Initialize(fftSize);
        return analysis;
    }

    public void Initialize(int fftSize)
    {
        FftSize = fftSize;
        InverseFft.InitializeRealToComplex(fftSize, LogSpectrum, Cepstrum);
        ForwardFft.InitializeComplexToComplex(
            fftSize, Cepstrum, MinimumPhaseSpectrum, FftDirection.Forward);
    }

    public readonly void GetMinimumPhaseSpectrum()
    {
        for (int i = (FftSize / 2) + 1; i < FftSize; ++i)
        {
            LogSpectrum[i] = LogSpectrum[FftSize - i];
        }

        InverseFft.Execute();
        Cepstrum[0].Imaginary *= -1.0;
        for (int i = 1; i < FftSize / 2; ++i)
        {
            Cepstrum[i].Real *= 2.0;
            Cepstrum[i].Imaginary *= -2.0;
        }
        Cepstrum[FftSize / 2].Imaginary *= -1.0;
        for (int i = (FftSize / 2) + 1; i < FftSize; ++i)
        {
            Cepstrum[i].Real = 0.0;
            Cepstrum[i].Imaginary = 0.0;
        }

        ForwardFft.Execute();

        for (int i = 0; i <= FftSize / 2; ++i)
        {
            double tmp = Math.Exp(MinimumPhaseSpectrum[i].Real / FftSize);
            MinimumPhaseSpectrum[i].Real =
                tmp * Math.Cos(MinimumPhaseSpectrum[i].Imaginary / FftSize);
            MinimumPhaseSpectrum[i].Imaginary =
                tmp * Math.Sin(MinimumPhaseSpectrum[i].Imaginary / FftSize);
        }
    }
}
