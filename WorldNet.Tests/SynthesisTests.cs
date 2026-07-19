namespace WorldNet.Tests;

public class SynthesisTests
{
    [Fact]
    public void SynthesizeMatchesReferenceWithinTolerance()
    {
        double[] meta = ReferenceData.Load("meta").Values;
        int fs = (int)meta[0];
        double framePeriod = meta[2];
        int fftSize = (int)meta[3];
        double[] f0 = ReferenceData.Load("stonemask_f0").Values;
        ReferenceArray spectrogram = ReferenceData.Load("cheaptrick_spectrogram");
        ReferenceArray aperiodicity = ReferenceData.Load("d4c_aperiodicity");
        double[] expected = ReferenceData.Load("synthesis_y").Values;

        int f0Length = f0.Length;
        int yLength = (int)((f0Length - 1) * framePeriod / 1000.0 * fs) + 1;
        Assert.Equal(expected.Length, yLength);

        double[] y = new double[yLength];
        using WorldArena arena = new();
        Synthesis.Synthesize(f0, spectrogram.Values, aperiodicity.Values, fftSize, framePeriod,
            fs, y, arena);

        long mismatches = 0;
        long maxUlp = 0;
        for (int i = 0; i < yLength; ++i)
        {
            long a = BitConverter.DoubleToInt64Bits(expected[i]);
            long b = BitConverter.DoubleToInt64Bits(y[i]);
            if (a != b)
            {
                ++mismatches;
                maxUlp = Math.Max(maxUlp, Math.Abs(a - b));
            }
        }
        if (maxUlp > 64)
        {
            Assert.Fail($"maxUlp={maxUlp} exceeds the tolerance inherited from MSVC pow.");
        }

        if (mismatches * 20 >= yLength)
        {
            Assert.Fail($"mismatches={mismatches} of {yLength} exceeds 5 percent.");
        }
    }

    [Fact]
    public void SynthesizeRejectsNullArena()
    {
        double[] f0 = new double[5];
        double[] spectrogram = new double[5 * 513];
        double[] aperiodicity = new double[5 * 513];
        double[] y = new double[100];

        Assert.Throws<ArgumentNullException>(
            () => Synthesis.Synthesize(f0, spectrogram, aperiodicity, 1024, 5.0, 44100, y,
                null!));
    }

    [Fact]
    public void SynthesizeRejectsShortContour()
    {
        using WorldArena arena = new();
        double[] f0 = new double[1];
        double[] spectrogram = new double[513];
        double[] aperiodicity = new double[513];
        double[] y = new double[100];

        Assert.Throws<ArgumentException>(
            () => Synthesis.Synthesize(f0, spectrogram, aperiodicity, 1024, 5.0, 44100, y,
                arena));
    }

    [Fact]
    public void SynthesizeRejectsSmallSpectrogram()
    {
        using WorldArena arena = new();
        double[] f0 = new double[5];
        double[] spectrogram = new double[(5 * 513) - 1];
        double[] aperiodicity = new double[5 * 513];
        double[] y = new double[100];

        Assert.Throws<ArgumentException>(
            () => Synthesis.Synthesize(f0, spectrogram, aperiodicity, 1024, 5.0, 44100, y,
                arena));
    }

    [Fact]
    public void SynthesizeRejectsEmptyDestination()
    {
        using WorldArena arena = new();
        double[] f0 = new double[5];
        double[] spectrogram = new double[5 * 513];
        double[] aperiodicity = new double[5 * 513];

        Assert.Throws<ArgumentException>(
            () => Synthesis.Synthesize(f0, spectrogram, aperiodicity, 1024, 5.0, 44100, [],
                arena));
    }
}
