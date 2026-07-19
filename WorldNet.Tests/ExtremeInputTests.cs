namespace WorldNet.Tests;

public class ExtremeInputTests
{
    public static TheoryData<int, int> ShortCases()
    {
        TheoryData<int, int> data = [];
        foreach (int fs in new[] { 8000, 22050, 44100 })
        {
            foreach (int length in new[] { 1, 2, 3, 8, 16, 32 })
            {
                data.Add(fs, length);
            }
        }
        return data;
    }

    private static double[] Ramp(int length)
    {
        double[] x = new double[length];
        for (int i = 0; i < length; ++i)
        {
            x[i] = Math.Sin(i * 0.3) * 0.5;
        }
        return x;
    }

    [Theory]
    [MemberData(nameof(ShortCases))]
    public void DioSurvivesVeryShortInput(int fs, int length)
    {
        double[] x = Ramp(length);
        using WorldArena arena = new();
        DioOption option = DioOption.Default;
        int f0Length = Dio.GetSamplesForDio(fs, length, option.FramePeriod);
        double[] positions = new double[f0Length];
        double[] f0 = new double[f0Length];

        Dio.Estimate(x, fs, option, positions, f0, arena);

        for (int i = 0; i < f0Length; ++i)
        {
            Assert.True(double.IsFinite(f0[i]));
        }
    }

    [Theory]
    [MemberData(nameof(ShortCases))]
    public void HarvestSurvivesVeryShortInput(int fs, int length)
    {
        double[] x = Ramp(length);
        using WorldArena arena = new();
        HarvestOption option = HarvestOption.Default;
        int f0Length = Harvest.GetSamplesForHarvest(fs, length, option.FramePeriod);
        double[] positions = new double[f0Length];
        double[] f0 = new double[f0Length];

        Harvest.Estimate(x, fs, option, positions, f0, arena);

        for (int i = 0; i < f0Length; ++i)
        {
            Assert.True(double.IsFinite(f0[i]));
        }
    }
}
