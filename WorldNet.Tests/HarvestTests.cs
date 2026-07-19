namespace WorldNet.Tests;

public class HarvestTests
{
    [Fact]
    public void EstimateMatchesReference()
    {
        double[] meta = ReferenceData.Load("meta").Values;
        int fs = (int)meta[0];
        double framePeriod = meta[2];
        double[] x = ReferenceData.Load("input_x").Values;
        double[] expectedPositions = ReferenceData.Load("harvest_temporal_positions").Values;
        double[] expectedF0 = ReferenceData.Load("harvest_f0").Values;

        Assert.Equal((int)meta[1], x.Length);
        int f0Length = Harvest.GetSamplesForHarvest(fs, x.Length, framePeriod);
        Assert.Equal(expectedF0.Length, f0Length);
        Assert.Equal(expectedPositions.Length, f0Length);

        double[] positions = new double[f0Length];
        double[] f0 = new double[f0Length];
        using WorldArena arena = new();

        HarvestOption option = HarvestOption.Default with { FramePeriod = framePeriod };
        Harvest.Estimate(x, fs, option, positions, f0, arena);

        for (int i = 0; i < f0Length; ++i)
        {
            if (BitConverter.DoubleToInt64Bits(expectedPositions[i])
                != BitConverter.DoubleToInt64Bits(positions[i]))
            {
                Assert.Fail(
                    $"position {i}: expected {expectedPositions[i]:E17} but was {positions[i]:E17}");
            }
        }

        for (int i = 0; i < f0Length; ++i)
        {
            if (BitConverter.DoubleToInt64Bits(expectedF0[i])
                != BitConverter.DoubleToInt64Bits(f0[i]))
            {
                Assert.Fail($"f0 {i}: expected {expectedF0[i]:E17} but was {f0[i]:E17}");
            }
        }
    }

    [Fact]
    public void GetSamplesForHarvestMatchesReference()
    {
        double[] expected = ReferenceData.Load("opt_samples_for_harvest").Values;
        int[] rates = [8000, 16000, 44100, 48000];
        double[] periods = [1.0, 2.5, 5.0, 10.0];
        int[] lengths = [1000, 12345, 48000, 100000];

        int index = 0;
        for (int r = 0; r < rates.Length; ++r)
        {
            for (int p = 0; p < periods.Length; ++p)
            {
                for (int l = 0; l < lengths.Length; ++l)
                {
                    int actual = Harvest.GetSamplesForHarvest(rates[r], lengths[l], periods[p]);
                    Assert.Equal((int)expected[index], actual);
                    ++index;
                }
            }
        }
    }

    [Fact]
    public void EstimateRejectsNullArena()
    {
        double[] x = new double[1000];
        double[] positions = new double[201];
        double[] f0 = new double[201];

        Assert.Throws<ArgumentNullException>(
            () => Harvest.Estimate(x, 44100, HarvestOption.Default, positions, f0, null!));
    }

    [Fact]
    public void EstimateRejectsEmptyWaveform()
    {
        using WorldArena arena = new();

        Assert.Throws<ArgumentException>(
            () => Harvest.Estimate([], 44100, HarvestOption.Default, [], [], arena));
    }

    [Fact]
    public void EstimateRejectsNonPositiveSampleRate()
    {
        using WorldArena arena = new();
        double[] x = new double[1000];

        Assert.Throws<ArgumentOutOfRangeException>(
            () => Harvest.Estimate(x, 0, HarvestOption.Default, [], [], arena));
    }

    [Fact]
    public void EstimateRejectsShortDestination()
    {
        using WorldArena arena = new();
        double[] x = new double[1000];
        int f0Length = Harvest.GetSamplesForHarvest(44100, x.Length, HarvestOption.Default.FramePeriod);
        double[] positions = new double[f0Length];
        double[] f0 = new double[f0Length - 1];

        Assert.Throws<ArgumentException>(
            () => Harvest.Estimate(x, 44100, HarvestOption.Default, positions, f0, arena));
    }
}
