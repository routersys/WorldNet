namespace WorldNet.Tests;

public class D4CTests
{
    [Fact]
    public void EstimateMatchesReferenceWithinOneUlp()
    {
        double[] meta = ReferenceData.Load("meta").Values;
        int fs = (int)meta[0];
        int fftSize = (int)meta[3];
        double[] x = ReferenceData.Load("input_x").Values;
        double[] temporalPositions = ReferenceData.Load("dio_temporal_positions").Values;
        double[] f0 = ReferenceData.Load("stonemask_f0").Values;
        ReferenceArray expected = ReferenceData.Load("d4c_aperiodicity");

        int f0Length = f0.Length;
        int spectrumLength = (fftSize / 2) + 1;
        Assert.Equal(f0Length, expected.Rows);
        Assert.Equal(spectrumLength, expected.Columns);

        double[] aperiodicity = new double[f0Length * spectrumLength];
        using WorldArena arena = new();
        D4C.Estimate(x, fs, D4COption.Default, temporalPositions, f0, fftSize, aperiodicity,
            arena);

        long mismatches = 0;
        long maxUlp = 0;
        for (int i = 0; i < expected.Values.Length; ++i)
        {
            long a = BitConverter.DoubleToInt64Bits(expected.Values[i]);
            long b = BitConverter.DoubleToInt64Bits(aperiodicity[i]);
            if (a != b)
            {
                ++mismatches;
                maxUlp = Math.Max(maxUlp, Math.Abs(a - b));
            }
        }

        if (maxUlp > 1)
        {
            Assert.Fail($"maxUlp={maxUlp} exceeds the one ULP tolerance of Math.Pow.");
        }

        if (mismatches * 1000 >= expected.Values.Length)
        {
            Assert.Fail(
                $"mismatches={mismatches} of {expected.Values.Length} exceeds 0.1 percent.");
        }
    }

    [Fact]
    public void OptionMatchesReference()
    {
        double expected = ReferenceData.Load("opt_d4c").Values[0];

        Assert.Equal(expected, D4COption.Default.Threshold);
    }

    [Fact]
    public void EstimateRejectsNullArena()
    {
        double[] x = new double[1000];

        Assert.Throws<ArgumentNullException>(
            () => D4C.Estimate(x, 44100, D4COption.Default, [], [], 1024, [], null!));
    }

    [Fact]
    public void EstimateRejectsMismatchedLengths()
    {
        using WorldArena arena = new();
        double[] x = new double[1000];
        double[] positions = new double[4];
        double[] f0 = new double[5];
        double[] aperiodicity = new double[5 * ((1024 / 2) + 1)];

        Assert.Throws<ArgumentException>(
            () => D4C.Estimate(x, 44100, D4COption.Default, positions, f0, 1024, aperiodicity,
                arena));
    }

    [Fact]
    public void EstimateRejectsSmallAperiodicity()
    {
        using WorldArena arena = new();
        double[] x = new double[1000];
        double[] positions = new double[5];
        double[] f0 = new double[5];
        double[] aperiodicity = new double[(5 * ((1024 / 2) + 1)) - 1];

        Assert.Throws<ArgumentException>(
            () => D4C.Estimate(x, 44100, D4COption.Default, positions, f0, 1024, aperiodicity,
                arena));
    }
}
