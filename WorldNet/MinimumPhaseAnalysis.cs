using System.Runtime.CompilerServices;

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

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public readonly void GetMinimumPhaseSpectrum()
    {
        for (int i = (FftSize / 2) + 1; i < FftSize; ++i)
        {
            LogSpectrum[i] = LogSpectrum[FftSize - i];
        }

        InverseFft.Execute();
        Cepstrum[0].Imaginary *= -1.0;
        VectorOperations.ScaleComplex((double*)(Cepstrum + 1), (FftSize / 2) - 1, 2.0, -2.0);
        Cepstrum[FftSize / 2].Imaginary *= -1.0;
        new Span<double>((double*)(Cepstrum + (FftSize / 2) + 1),
            (FftSize - (FftSize / 2) - 1) * 2).Clear();

        ForwardFft.Execute();

        double inverseFftSize = 1.0 / FftSize;
        for (int i = 0; i <= FftSize / 2; ++i)
        {
            double tmp = Math.Exp(MinimumPhaseSpectrum[i].Real * inverseFftSize);
            (double sine, double cosine) =
                WorldMath.SinCos(MinimumPhaseSpectrum[i].Imaginary * inverseFftSize);
            MinimumPhaseSpectrum[i].Real = tmp * cosine;
            MinimumPhaseSpectrum[i].Imaginary = tmp * sine;
        }
    }
}
