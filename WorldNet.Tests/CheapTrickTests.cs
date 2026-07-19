namespace WorldNet.Tests;

public class CheapTrickTests
{
    private static readonly int[] Rates =
        [8000, 16000, 22050, 24000, 32000, 44100, 48000, 96000];

    [Fact]
    public void EstimateMatchesReference()
    {
        double[] meta = ReferenceData.Load("meta").Values;
        int fs = (int)meta[0];
        int fftSize = (int)meta[3];
        double[] x = ReferenceData.Load("input_x").Values;
        double[] temporalPositions = ReferenceData.Load("dio_temporal_positions").Values;
        double[] f0 = ReferenceData.Load("stonemask_f0").Values;
        ReferenceArray expected = ReferenceData.Load("cheaptrick_spectrogram");

        int f0Length = f0.Length;
        int spectrumLength = (fftSize / 2) + 1;
        Assert.Equal(f0Length, expected.Rows);
        Assert.Equal(spectrumLength, expected.Columns);

        CheapTrickOption option = CheapTrickOption.Create(fs);
        Assert.Equal(fftSize, option.FftSize);

        double[] spectrogram = new double[f0Length * spectrumLength];
        using WorldArena arena = new();
        CheapTrick.Estimate(x, fs, option, temporalPositions, f0, spectrogram, arena);

        for (int i = 0; i < expected.Values.Length; ++i)
        {
            if (BitConverter.DoubleToInt64Bits(expected.Values[i])
                != BitConverter.DoubleToInt64Bits(spectrogram[i]))
            {
                Assert.Fail(
                    $"frame {i / spectrumLength} bin {i % spectrumLength}: " +
                    $"expected {expected.Values[i]:E17} but was {spectrogram[i]:E17}");
            }
        }
    }

    [Fact]
    public void OptionMatchesReference()
    {
        double[] expected = ReferenceData.Load("opt_cheaptrick").Values;
        int fs = (int)ReferenceData.Load("meta").Values[0];

        CheapTrickOption option = CheapTrickOption.Create(fs);

        Assert.Equal(expected[0], option.Q1);
        Assert.Equal(expected[1], option.F0Floor);
        Assert.Equal((int)expected[2], option.FftSize);
    }

    [Fact]
    public void GetFftSizeMatchesReference()
    {
        double[] expected = ReferenceData.Load("opt_fft_size_by_rate").Values;

        for (int i = 0; i < Rates.Length; ++i)
        {
            Assert.Equal((int)expected[i], CheapTrick.GetFftSize(Rates[i], WorldConstants.FloorF0));
        }
    }

    [Fact]
    public void GetF0FloorMatchesReference()
    {
        double[] expected = ReferenceData.Load("opt_f0_floor_by_rate").Values;

        for (int i = 0; i < Rates.Length; ++i)
        {
            int fftSize = CheapTrick.GetFftSize(Rates[i], WorldConstants.FloorF0);
            Assert.Equal(expected[i], CheapTrick.GetF0Floor(Rates[i], fftSize));
        }
    }

    [Fact]
    public void EstimateRejectsNullArena()
    {
        double[] x = new double[1000];
        CheapTrickOption option = CheapTrickOption.Create(44100);

        Assert.Throws<ArgumentNullException>(
            () => CheapTrick.Estimate(x, 44100, option, [], [], [], null!));
    }

    [Fact]
    public void EstimateRejectsMismatchedLengths()
    {
        using WorldArena arena = new();
        double[] x = new double[1000];
        CheapTrickOption option = CheapTrickOption.Create(44100);
        double[] positions = new double[4];
        double[] f0 = new double[5];
        double[] spectrogram = new double[5 * ((option.FftSize / 2) + 1)];

        Assert.Throws<ArgumentException>(
            () => CheapTrick.Estimate(x, 44100, option, positions, f0, spectrogram, arena));
    }

    [Fact]
    public void EstimateRejectsSmallSpectrogram()
    {
        using WorldArena arena = new();
        double[] x = new double[1000];
        CheapTrickOption option = CheapTrickOption.Create(44100);
        double[] positions = new double[5];
        double[] f0 = new double[5];
        double[] spectrogram = new double[(5 * ((option.FftSize / 2) + 1)) - 1];

        Assert.Throws<ArgumentException>(
            () => CheapTrick.Estimate(x, 44100, option, positions, f0, spectrogram, arena));
    }
}
