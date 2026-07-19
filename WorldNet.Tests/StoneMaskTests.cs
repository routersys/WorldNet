namespace WorldNet.Tests;

public class StoneMaskTests
{
    [Fact]
    public void RefineMatchesReference()
    {
        double[] meta = ReferenceData.Load("meta").Values;
        int fs = (int)meta[0];
        double[] x = ReferenceData.Load("input_x").Values;
        double[] temporalPositions = ReferenceData.Load("dio_temporal_positions").Values;
        double[] f0 = ReferenceData.Load("dio_f0").Values;
        double[] expected = ReferenceData.Load("stonemask_f0").Values;

        Assert.Equal((int)meta[1], x.Length);
        Assert.Equal(expected.Length, f0.Length);

        double[] refined = new double[f0.Length];
        using WorldArena arena = new();

        StoneMask.Refine(x, fs, temporalPositions, f0, refined, arena);

        for (int i = 0; i < expected.Length; ++i)
        {
            if (BitConverter.DoubleToInt64Bits(expected[i])
                != BitConverter.DoubleToInt64Bits(refined[i]))
            {
                Assert.Fail($"frame {i}: expected {expected[i]:E17} but was {refined[i]:E17}");
            }
        }
    }

    [Fact]
    public void RefineRejectsMismatchedLengths()
    {
        using WorldArena arena = new();
        double[] x = new double[100];
        double[] positions = new double[4];
        double[] f0 = new double[5];
        double[] refined = new double[5];

        Assert.Throws<ArgumentException>(
            () => StoneMask.Refine(x, 44100, positions, f0, refined, arena));
    }

    [Fact]
    public void RefineRejectsShortDestination()
    {
        using WorldArena arena = new();
        double[] x = new double[100];
        double[] positions = new double[5];
        double[] f0 = new double[5];
        double[] refined = new double[4];

        Assert.Throws<ArgumentException>(
            () => StoneMask.Refine(x, 44100, positions, f0, refined, arena));
    }

    [Fact]
    public void RefineRejectsEmptyWaveform()
    {
        using WorldArena arena = new();

        Assert.Throws<ArgumentException>(
            () => StoneMask.Refine([], 44100, [], [], [], arena));
    }

    [Fact]
    public void RefineRejectsNonPositiveSampleRate()
    {
        using WorldArena arena = new();
        double[] x = new double[100];

        Assert.Throws<ArgumentOutOfRangeException>(
            () => StoneMask.Refine(x, 0, [], [], [], arena));
    }

    [Fact]
    public void RefineAcceptsEmptyContour()
    {
        using WorldArena arena = new();
        double[] x = new double[100];

        StoneMask.Refine(x, 44100, [], [], [], arena);
    }
}
