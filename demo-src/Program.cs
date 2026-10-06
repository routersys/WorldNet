using System.Diagnostics;
using System.Runtime.InteropServices.JavaScript;
using System.Text;
using WorldNet;

public static partial class Demo
{
    private const double FramePeriod = 5.0;
    private static double[] _f0 = [];
    private static string _timings = "";

    [JSExport]
    [return: JSMarshalAs<JSType.Array<JSType.Number>>]
    public static double[] GetF0()
    {
        return _f0;
    }

    [JSExport]
    public static string GetTimings()
    {
        return _timings;
    }

    [JSExport]
    public static byte[] Process(byte[] wave, double semitones)
    {
        const string inputPath = "/input.wav";
        const string outputPath = "/output.wav";
        File.WriteAllBytes(inputPath, wave);

        StringBuilder log = new();
        Stopwatch watch = Stopwatch.StartNew();
        void Lap(string stage)
        {
            log.Append(stage).Append(' ').Append(watch.Elapsed.TotalMilliseconds.ToString("F0")).Append(" ms\n");
            watch.Restart();
        }

        int length = WaveFile.GetLength(inputPath);
        double[] x = new double[length];
        WaveFile.Read(inputPath, x, out int fs, out _);
        using WorldArena arena = new();

        HarvestOption harvestOption = HarvestOption.Default with { FramePeriod = FramePeriod };
        int f0Length = Harvest.GetSamplesForHarvest(fs, x.Length, FramePeriod);
        double[] positions = new double[f0Length];
        double[] f0 = new double[f0Length];
        Harvest.Estimate(x, fs, harvestOption, positions, f0, arena);
        Lap("Harvest");

        CheapTrickOption cheapTrickOption = CheapTrickOption.Create(fs);
        int fftSize = cheapTrickOption.FftSize;
        int spectrumLength = (fftSize / 2) + 1;
        double[] spectrogram = new double[f0Length * spectrumLength];
        CheapTrick.Estimate(x, fs, cheapTrickOption, positions, f0, spectrogram, arena);
        Lap("CheapTrick");

        double[] aperiodicity = new double[f0Length * spectrumLength];
        D4C.Estimate(x, fs, D4COption.Default, positions, f0, fftSize, aperiodicity, arena);
        Lap("D4C");

        _f0 = (double[])f0.Clone();
        double ratio = Math.Pow(2.0, semitones / 12.0);
        for (int i = 0; i < f0.Length; ++i)
        {
            f0[i] *= ratio;
        }

        int yLength = Synthesis.GetSamplesForSynthesis(fs, f0Length, FramePeriod);
        double[] y = new double[yLength];
        Synthesis.Synthesize(f0, spectrogram, aperiodicity, fftSize, FramePeriod, fs, y, arena);
        Lap("Synthesis");

        WaveFile.Write(outputPath, y, fs);
        _timings = log.ToString();
        return File.ReadAllBytes(outputPath);
    }

    public static void Main()
    {
    }
}
