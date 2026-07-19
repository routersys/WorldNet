namespace WorldNet.Tests;

public unsafe class OouraFftTests
{
    [Theory]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(64)]
    [InlineData(128)]
    [InlineData(256)]
    [InlineData(512)]
    [InlineData(1024)]
    [InlineData(2048)]
    [InlineData(4096)]
    public void RealToComplexMatchesReference(int n)
    {
        double[] source = ReferenceData.Load("fft_real_input").Values;
        ReferenceArray expected = ReferenceData.Load($"fft_r2c_{n}");

        using WorldArena arena = new(1 << 21);
        double* waveform = (double*)arena.AllocateRaw(n, sizeof(double));
        FftComplex* spectrum = (FftComplex*)arena.AllocateRaw(n / 2 + 1, (nuint)sizeof(FftComplex));
        for (int i = 0; i < n; ++i)
        {
            waveform[i] = source[i];
        }

        FftPlan plan = FftPlan.CreateRealToComplex(n, waveform, spectrum, arena);
        plan.Execute();

        Assert.Equal(n / 2 + 1, expected.Rows);
        double[] actual = new double[(n / 2 + 1) * 2];
        for (int i = 0; i <= n / 2; ++i)
        {
            actual[i * 2] = spectrum[i].Real;
            actual[i * 2 + 1] = spectrum[i].Imaginary;
        }

        AssertMatches(expected.Values, actual, $"r2c n={n}");
    }

    [Theory]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(64)]
    [InlineData(128)]
    [InlineData(256)]
    [InlineData(512)]
    [InlineData(1024)]
    [InlineData(2048)]
    [InlineData(4096)]
    public void ComplexToRealMatchesReference(int n)
    {
        double[] source = ReferenceData.Load("fft_complex_input").Values;
        ReferenceArray expected = ReferenceData.Load($"fft_c2r_{n}");

        using WorldArena arena = new(1 << 21);
        FftComplex* spectrum = (FftComplex*)arena.AllocateRaw(n, (nuint)sizeof(FftComplex));
        double* waveform = (double*)arena.AllocateRaw(n, sizeof(double));
        for (int i = 0; i < n; ++i)
        {
            spectrum[i].Real = source[i * 2];
            spectrum[i].Imaginary = source[i * 2 + 1];
        }

        FftPlan plan = FftPlan.CreateComplexToReal(n, spectrum, waveform, arena);
        plan.Execute();

        double[] actual = new double[n];
        for (int i = 0; i < n; ++i)
        {
            actual[i] = waveform[i];
        }

        AssertMatches(expected.Values, actual, $"c2r n={n}");
    }

    [Theory]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(64)]
    [InlineData(128)]
    [InlineData(256)]
    [InlineData(512)]
    [InlineData(1024)]
    [InlineData(2048)]
    [InlineData(4096)]
    public void ComplexToComplexForwardMatchesReference(int n)
    {
        AssertComplexToComplex(n, FftDirection.Forward, $"fft_c2c_forward_{n}");
    }

    [Theory]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(64)]
    [InlineData(128)]
    [InlineData(256)]
    [InlineData(512)]
    [InlineData(1024)]
    [InlineData(2048)]
    [InlineData(4096)]
    public void ComplexToComplexBackwardMatchesReference(int n)
    {
        AssertComplexToComplex(n, FftDirection.Backward, $"fft_c2c_backward_{n}");
    }

    private static void AssertComplexToComplex(int n, FftDirection direction, string referenceName)
    {
        double[] source = ReferenceData.Load("fft_complex_input").Values;
        ReferenceArray expected = ReferenceData.Load(referenceName);

        using WorldArena arena = new(1 << 21);
        FftComplex* input = (FftComplex*)arena.AllocateRaw(n, (nuint)sizeof(FftComplex));
        FftComplex* output = (FftComplex*)arena.AllocateRaw(n, (nuint)sizeof(FftComplex));
        for (int i = 0; i < n; ++i)
        {
            input[i].Real = source[i * 2];
            input[i].Imaginary = source[i * 2 + 1];
        }

        FftPlan plan = FftPlan.CreateComplexToComplex(n, input, output, direction, arena);
        plan.Execute();

        double[] actual = new double[n * 2];
        for (int i = 0; i < n; ++i)
        {
            actual[i * 2] = output[i].Real;
            actual[i * 2 + 1] = output[i].Imaginary;
        }

        AssertMatches(expected.Values, actual, $"c2c {direction} n={n}");
    }

    private static void AssertMatches(double[] expected, double[] actual, string label)
    {
        Assert.Equal(expected.Length, actual.Length);

        long maxUlp = 0;
        double maxAbsolute = 0.0;
        int worstIndex = -1;
        for (int i = 0; i < expected.Length; ++i)
        {
            long ulp = UlpDifference(expected[i], actual[i]);
            if (ulp > maxUlp)
            {
                maxUlp = ulp;
                worstIndex = i;
            }

            double absolute = Math.Abs(expected[i] - actual[i]);
            if (absolute > maxAbsolute)
            {
                maxAbsolute = absolute;
            }
        }

        Assert.True(
            maxUlp == 0,
            $"{label}: max ULP {maxUlp} at index {worstIndex}, max absolute difference {maxAbsolute:E17}");
    }

    private static long UlpDifference(double expected, double actual)
    {
        return Math.Abs(ToOrderedBits(expected) - ToOrderedBits(actual));
    }

    private static long ToOrderedBits(double value)
    {
        long bits = BitConverter.DoubleToInt64Bits(value);
        return bits < 0 ? (long)(0x8000000000000000UL - (ulong)bits) : bits;
    }
}
