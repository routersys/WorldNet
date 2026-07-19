namespace WorldNet.Tests;

public class DegenerateInputTests
{
    public static TheoryData<int, string, int> Cases()
    {
        TheoryData<int, string, int> data = [];
        foreach (int fs in new[] { 8000, 16000, 22050, 44100, 48000 })
        {
            foreach (string kind in new[] { "silence", "dc", "noise", "impulse" })
            {
                foreach (int length in new[] { 64, 512, 4000 })
                {
                    data.Add(fs, kind, length);
                }
            }
        }
        return data;
    }

    private static double[] MakeSignal(string kind, int length)
    {
        double[] x = new double[length];
        uint seed = 2463534242u;
        for (int i = 0; i < length; ++i)
        {
            switch (kind)
            {
                case "silence":
                    x[i] = 0.0;
                    break;
                case "dc":
                    x[i] = 0.5;
                    break;
                case "impulse":
                    x[i] = i == length / 2 ? 1.0 : 0.0;
                    break;
                default:
                    seed ^= seed << 13;
                    seed ^= seed >> 17;
                    seed ^= seed << 5;
                    x[i] = (seed / 4294967296.0 * 2.0) - 1.0;
                    break;
            }
        }
        return x;
    }

    private static void AssertFinite(ReadOnlySpan<double> values, string label)
    {
        for (int i = 0; i < values.Length; ++i)
        {
            if (!double.IsFinite(values[i]))
            {
                Assert.Fail($"{label}[{i}] is {values[i]}");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void DioPipelineSurvivesDegenerateInput(int fs, string kind, int length)
    {
        double[] x = MakeSignal(kind, length);
        using WorldArena arena = new();

        DioOption dioOption = DioOption.Default;
        int f0Length = Dio.GetSamplesForDio(fs, length, dioOption.FramePeriod);
        double[] positions = new double[f0Length];
        double[] f0 = new double[f0Length];
        Dio.Estimate(x, fs, dioOption, positions, f0, arena);
        AssertFinite(f0, "f0");
        AssertFinite(positions, "positions");

        double[] refined = new double[f0Length];
        StoneMask.Refine(x, fs, positions, f0, refined, arena);
        AssertFinite(refined, "refined");

        CheapTrickOption ctOption = CheapTrickOption.Create(fs);
        int spectrumLength = (ctOption.FftSize / 2) + 1;
        double[] spectrogram = new double[f0Length * spectrumLength];
        CheapTrick.Estimate(x, fs, ctOption, positions, refined, spectrogram, arena);
        AssertFinite(spectrogram, "spectrogram");
        foreach (double v in spectrogram)
        {
            Assert.True(v > 0.0, "the spectral envelope must stay positive");
        }

        double[] aperiodicity = new double[f0Length * spectrumLength];
        D4C.Estimate(x, fs, D4COption.Default, positions, refined, ctOption.FftSize,
            aperiodicity, arena);
        AssertFinite(aperiodicity, "aperiodicity");
        foreach (double v in aperiodicity)
        {
            Assert.InRange(v, 0.0, 1.0);
        }

        if (f0Length >= 2)
        {
            int yLength = (int)((f0Length - 1) * dioOption.FramePeriod / 1000.0 * fs) + 1;
            double[] y = new double[yLength];
            Synthesis.Synthesize(refined, spectrogram, aperiodicity, ctOption.FftSize,
                dioOption.FramePeriod, fs, y, arena);
            AssertFinite(y, "y");
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void HarvestSurvivesDegenerateInput(int fs, string kind, int length)
    {
        double[] x = MakeSignal(kind, length);
        using WorldArena arena = new();

        HarvestOption option = HarvestOption.Default;
        int f0Length = Harvest.GetSamplesForHarvest(fs, length, option.FramePeriod);
        double[] positions = new double[f0Length];
        double[] f0 = new double[f0Length];
        Harvest.Estimate(x, fs, option, positions, f0, arena);
        AssertFinite(f0, "harvest f0");
        AssertFinite(positions, "harvest positions");
    }
}
