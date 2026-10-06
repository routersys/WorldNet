namespace WorldNet.Tests;

public class SizingHelperTests
{
    [Theory]
    [InlineData(0, 100, 5.0)]
    [InlineData(-1, 100, 5.0)]
    [InlineData(44100, -1, 5.0)]
    [InlineData(44100, 100, 0.0)]
    [InlineData(44100, 100, -5.0)]
    [InlineData(44100, 100, double.NaN)]
    public void GetSamplesForDioRejectsInvalidArguments(int fs, int xLength, double framePeriod)
    {
        Assert.ThrowsAny<ArgumentException>(() => Dio.GetSamplesForDio(fs, xLength, framePeriod));
    }

    [Theory]
    [InlineData(0, 100, 5.0)]
    [InlineData(44100, -1, 5.0)]
    [InlineData(44100, 100, 0.0)]
    [InlineData(44100, 100, double.NaN)]
    public void GetSamplesForHarvestRejectsInvalidArguments(int fs, int xLength,
        double framePeriod)
    {
        Assert.ThrowsAny<ArgumentException>(
            () => Harvest.GetSamplesForHarvest(fs, xLength, framePeriod));
    }

    [Theory]
    [InlineData(0, 10, 5.0)]
    [InlineData(44100, 0, 5.0)]
    [InlineData(44100, -3, 5.0)]
    [InlineData(44100, 10, 0.0)]
    [InlineData(44100, 10, double.NaN)]
    public void GetSamplesForSynthesisRejectsInvalidArguments(int fs, int f0Length,
        double framePeriod)
    {
        Assert.ThrowsAny<ArgumentException>(
            () => Synthesis.GetSamplesForSynthesis(fs, f0Length, framePeriod));
    }

    [Theory]
    [InlineData(0, 71.0)]
    [InlineData(44100, 0.0)]
    [InlineData(44100, -1.0)]
    [InlineData(44100, double.NaN)]
    public void GetFftSizeRejectsInvalidArguments(int fs, double f0Floor)
    {
        Assert.ThrowsAny<ArgumentException>(() => CheapTrick.GetFftSize(fs, f0Floor));
    }

    [Theory]
    [InlineData(0, 2048)]
    [InlineData(44100, 3)]
    [InlineData(44100, 0)]
    [InlineData(44100, -1)]
    public void GetF0FloorRejectsInvalidArguments(int fs, int fftSize)
    {
        Assert.ThrowsAny<ArgumentException>(() => CheapTrick.GetF0Floor(fs, fftSize));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-8000)]
    public void GetNumberOfAperiodicitiesRejectsNonPositiveSamplingRate(int fs)
    {
        Assert.ThrowsAny<ArgumentException>(() => Codec.GetNumberOfAperiodicities(fs));
    }
}
