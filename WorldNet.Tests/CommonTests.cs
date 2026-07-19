namespace WorldNet.Tests;

public unsafe class CommonTests
{
    private const int FftSize = 2048;
    private const int SampleRate = 44100;
    private const double TestF0 = 200.0;

    [Fact]
    public void GetSuitableFftSizeMatchesReference()
    {
        double[] expected = ReferenceData.Load("cm_suitable_fft_size").Values;

        for (int i = 0; i < expected.Length; ++i)
        {
            Assert.Equal(expected[i], Common.GetSuitableFftSize(i + 1));
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(8)]
    [InlineData(15)]
    [InlineData(64)]
    [InlineData(257)]
    [InlineData(1024)]
    public void NuttallWindowMatchesReference(int length)
    {
        double[] expected = ReferenceData.Load($"cm_nuttall_{length}").Values;

        using WorldArena arena = new();
        double* y = (double*)arena.AllocateRaw(length, sizeof(double));

        Common.NuttallWindow(length, y);

        AssertExact(expected, y, $"nuttall {length}");
    }

    [Fact]
    public void DcCorrectionMatchesReference()
    {
        double[] source = ReferenceData.Load("cm_power_spectrum").Values;
        double[] expected = ReferenceData.Load("cm_dc_correction").Values;
        int spectrumLength = (FftSize / 2) + 1;
        Assert.Equal(spectrumLength, source.Length);

        using WorldArena arena = new();
        double* powerSpectrum = Copy(arena, source);
        double* output = Copy(arena, source);

        int upperLimit = Common.GetDcCorrectionUpperLimit(TestF0, SampleRate, FftSize);
        DcCorrectionScratch scratch = DcCorrectionScratch.Bind(arena, upperLimit);
        Common.DcCorrection(powerSpectrum, TestF0, SampleRate, FftSize, output, scratch);

        AssertExact(expected, output, "DC correction");
    }

    [Fact]
    public void LinearSmoothingMatchesReference()
    {
        double[] source = ReferenceData.Load("cm_power_spectrum").Values;
        double[] expected = ReferenceData.Load("cm_linear_smoothing").Values;

        using WorldArena arena = new();
        double* powerSpectrum = Copy(arena, source);
        double* output = (double*)arena.AllocateRaw(expected.Length, sizeof(double));

        int boundary = Common.GetLinearSmoothingBoundary(TestF0, SampleRate, FftSize);
        LinearSmoothingScratch scratch = LinearSmoothingScratch.Bind(arena, FftSize, boundary);
        Common.LinearSmoothing(powerSpectrum, TestF0, SampleRate, FftSize, output, scratch);

        AssertExact(expected, output, "linear smoothing");
    }

    [Fact]
    public void MinimumPhaseSpectrumMatchesReference()
    {
        double[] source = ReferenceData.Load("cm_power_spectrum").Values;
        ReferenceArray expected = ReferenceData.Load("cm_minimum_phase");
        Assert.Equal((FftSize / 2) + 1, expected.Rows);

        using WorldArena arena = new();
        MinimumPhaseAnalysis analysis = MinimumPhaseAnalysis.Bind(arena, FftSize);
        for (int i = 0; i <= FftSize / 2; ++i)
        {
            analysis.LogSpectrum[i] = Math.Log(source[i]);
        }

        analysis.GetMinimumPhaseSpectrum();

        for (int i = 0; i <= FftSize / 2; ++i)
        {
            AssertBitEqual(expected.Values[i * 2], analysis.MinimumPhaseSpectrum[i].Real,
                $"minimum phase real {i}");
            AssertBitEqual(expected.Values[(i * 2) + 1], analysis.MinimumPhaseSpectrum[i].Imaginary,
                $"minimum phase imaginary {i}");
        }
    }

    [Fact]
    public void FastFftFiltMatchesReference()
    {
        double[] sourceX = ReferenceData.Load("mf_fastfftfilt_x").Values;
        double[] sourceH = ReferenceData.Load("mf_fastfftfilt_h").Values;
        double[] expected = ReferenceData.Load("mf_fastfftfilt_y").Values;
        int fftSize = expected.Length;

        using WorldArena arena = new();
        double* x = Copy(arena, sourceX);
        double* h = Copy(arena, sourceH);
        double* y = (double*)arena.AllocateRaw(fftSize, sizeof(double));

        ForwardRealFft forward = ForwardRealFft.Bind(arena, fftSize);
        InverseRealFft inverse = InverseRealFft.Bind(arena, fftSize);
        FastFftFiltScratch scratch = FastFftFiltScratch.Bind(arena, fftSize);

        MatlabFunctions.FastFftFilt(
            x, sourceX.Length, h, sourceH.Length, fftSize, forward, inverse, y, scratch);

        AssertExact(expected, y, "fast fftfilt");
    }

    private static double* Copy(WorldArena arena, double[] source)
    {
        double* target = (double*)arena.AllocateRaw(source.Length, sizeof(double));
        for (int i = 0; i < source.Length; ++i)
        {
            target[i] = source[i];
        }
        return target;
    }

    private static void AssertExact(double[] expected, double* actual, string label)
    {
        for (int i = 0; i < expected.Length; ++i)
        {
            AssertBitEqual(expected[i], actual[i], $"{label} index {i}");
        }
    }

    private static void AssertBitEqual(double expected, double actual, string label)
    {
        if (BitConverter.DoubleToInt64Bits(expected) != BitConverter.DoubleToInt64Bits(actual))
        {
            Assert.Fail($"{label}: expected {expected:E17} but was {actual:E17}");
        }
    }
}
