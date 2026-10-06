namespace WorldNet.Tests;

public class ArgumentValidationTests
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

    [Theory]
    [InlineData(900.0, 100.0)]
    [InlineData(200.0, 199.0)]
    [InlineData(71.0, double.PositiveInfinity)]
    public void DioRejectsInvalidF0Range(double floor, double ceil)
    {
        using WorldArena arena = new();
        double[] x = new double[4410];
        DioOption option = DioOption.Default with { F0Floor = floor, F0Ceil = ceil };
        double[] positions = new double[Dio.GetSamplesForDio(22050, x.Length, 5.0)];
        double[] f0 = new double[positions.Length];

        Assert.ThrowsAny<ArgumentException>(
            () => Dio.Estimate(x, 22050, option, positions, f0, arena));
    }

    [Theory]
    [InlineData(900.0, 100.0)]
    [InlineData(200.0, 199.0)]
    [InlineData(71.0, double.PositiveInfinity)]
    public void HarvestRejectsInvalidF0Range(double floor, double ceil)
    {
        using WorldArena arena = new();
        double[] x = new double[4410];
        HarvestOption option = HarvestOption.Default with { F0Floor = floor, F0Ceil = ceil };
        double[] positions = new double[Harvest.GetSamplesForHarvest(22050, x.Length, 5.0)];
        double[] f0 = new double[positions.Length];

        Assert.ThrowsAny<ArgumentException>(
            () => Harvest.Estimate(x, 22050, option, positions, f0, arena));
    }

    [Fact]
    public void DioAndHarvestAcceptEqualF0LimitsAndTheDefaultRange()
    {
        using WorldArena arena = new();
        double[] x = new double[4410];
        int length = Dio.GetSamplesForDio(22050, x.Length, 5.0);
        double[] positions = new double[length];
        double[] f0 = new double[length];

        Dio.Estimate(x, 22050, DioOption.Default with { F0Floor = 200.0, F0Ceil = 200.0 },
            positions, f0, arena);
        Harvest.Estimate(x, 22050, HarvestOption.Default with { F0Floor = 200.0, F0Ceil = 200.0 },
            positions, f0, arena);
        Dio.Estimate(x, 22050, DioOption.Default, positions, f0, arena);
        Harvest.Estimate(x, 22050, HarvestOption.Default, positions, f0, arena);
    }
}
