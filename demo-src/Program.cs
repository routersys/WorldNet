using System.Diagnostics;
using System.Runtime.InteropServices.JavaScript;
using WorldNet;

public static partial class Demo
{
    private const double FramePeriod = 5.0;
    private const double Margin = 0.000000000001;
    private const double MaximumPeak = 0.98;
    private const int Robot = 1;
    private const int Whisper = 2;
    private const int DioWithStoneMask = 1;

    private static readonly WorldArena Arena = new();
    private static double[] _x = [];
    private static int _fs;
    private static double[] _f0 = [];
    private static double[] _spectrogram = [];
    private static double[] _aperiodicity = [];
    private static double[] _decoded = [];
    private static int _fftSize;
    private static int _spectrumLength;
    private static double _lastMilliseconds;

    [JSExport]
    [return: JSMarshalAs<JSType.Array<JSType.Number>>]
    public static double[] AnalyzeWave(byte[] wave)
    {
        const string path = "/input.wav";
        File.WriteAllBytes(path, wave);
        double[] x = new double[WaveFile.GetLength(path)];
        WaveFile.Read(path, x, out int fs, out _);
        File.Delete(path);
        return Analyze(x, fs);
    }

    [JSExport]
    [return: JSMarshalAs<JSType.Array<JSType.Number>>]
    public static double[] AnalyzeSamples(byte[] samples, int fs)
    {
        double[] x = new double[samples.Length / sizeof(double)];
        Buffer.BlockCopy(samples, 0, x, 0, x.Length * sizeof(double));
        return Analyze(x, fs);
    }

    [JSExport]
    [return: JSMarshalAs<JSType.Array<JSType.Number>>]
    public static double[] GetInfo()
    {
        return [_fs, _f0.Length, _spectrumLength, _fftSize, _x.Length];
    }

    [JSExport]
    [return: JSMarshalAs<JSType.Array<JSType.Number>>]
    public static double[] GetF0()
    {
        return _f0;
    }

    [JSExport]
    public static double GetMilliseconds()
    {
        return _lastMilliseconds;
    }

    [JSExport]
    public static byte[] Convert(double semitones, double speed, double formant, double breathiness, int mode)
    {
        Stopwatch watch = Stopwatch.StartNew();
        int last = _f0.Length - 1;
        int frames = Math.Max(1, (int)Math.Round(_f0.Length / speed));
        double pitch = Math.Pow(2.0, semitones / 12.0);
        double robotF0 = mode == Robot ? MedianVoiced(_f0) : 0.0;
        double[] f0 = new double[frames];
        double[] spectrogram = new double[frames * _spectrumLength];
        double[] aperiodicity = new double[frames * _spectrumLength];
        for (int i = 0; i < frames; ++i)
        {
            double position = Math.Min(i * speed, last);
            int lower = (int)position;
            int upper = Math.Min(lower + 1, last);
            double weight = position - lower;
            f0[i] = mode switch
            {
                Whisper => 0.0,
                Robot => _f0[weight < 0.5 ? lower : upper] > 0.0 ? robotF0 * pitch : 0.0,
                _ => InterpolateF0(lower, upper, weight) * pitch,
            };
            for (int k = 0; k < _spectrumLength; ++k)
            {
                double bin = k / formant;
                spectrogram[(i * _spectrumLength) + k] = Mix(_spectrogram, lower, upper, weight, bin);
                aperiodicity[(i * _spectrumLength) + k] = Math.Clamp(
                    Mix(_aperiodicity, lower, upper, weight, k) * breathiness, Margin, 1.0 - Margin);
            }
        }

        byte[] wave = Render(f0, spectrogram, aperiodicity);
        _lastMilliseconds = watch.Elapsed.TotalMilliseconds;
        return wave;
    }

    [JSExport]
    public static byte[] ConvertCoded(int dimensions)
    {
        Stopwatch watch = Stopwatch.StartNew();
        int f0Length = _f0.Length;
        double[] coded = new double[f0Length * dimensions];
        Codec.CodeSpectralEnvelope(_spectrogram, f0Length, _fs, _fftSize, dimensions, coded, Arena);
        double[] decoded = new double[_spectrogram.Length];
        Codec.DecodeSpectralEnvelope(coded, f0Length, _fs, _fftSize, dimensions, decoded, Arena);
        _decoded = decoded;
        byte[] wave = Render(_f0, decoded, _aperiodicity);
        _lastMilliseconds = watch.Elapsed.TotalMilliseconds;
        return wave;
    }

    [JSExport]
    [return: JSMarshalAs<JSType.Array<JSType.Number>>]
    public static double[] GetEnvelope(int frame, int decoded)
    {
        double[] source = decoded != 0 ? _decoded : _spectrogram;
        return source.AsSpan(frame * _spectrumLength, _spectrumLength).ToArray();
    }

    [JSExport]
    [return: JSMarshalAs<JSType.Array<JSType.Number>>]
    public static double[] EstimateF0(int method)
    {
        Stopwatch watch = Stopwatch.StartNew();
        DioOption option = DioOption.Default with { FramePeriod = FramePeriod };
        int length = Dio.GetSamplesForDio(_fs, _x.Length, FramePeriod);
        double[] positions = new double[length];
        double[] f0 = new double[length];
        Dio.Estimate(_x, _fs, option, positions, f0, Arena);
        if (method == DioWithStoneMask)
        {
            double[] refined = new double[length];
            StoneMask.Refine(_x, _fs, positions, f0, refined, Arena);
            f0 = refined;
        }

        _lastMilliseconds = watch.Elapsed.TotalMilliseconds;
        return f0;
    }

    public static void Main()
    {
    }

    private static double[] Analyze(double[] x, int fs)
    {
        Stopwatch watch = Stopwatch.StartNew();
        HarvestOption harvestOption = HarvestOption.Default with { FramePeriod = FramePeriod };
        int f0Length = Harvest.GetSamplesForHarvest(fs, x.Length, FramePeriod);
        double[] positions = new double[f0Length];
        double[] f0 = new double[f0Length];
        Harvest.Estimate(x, fs, harvestOption, positions, f0, Arena);
        double harvest = Lap(watch);

        CheapTrickOption cheapTrickOption = CheapTrickOption.Create(fs);
        int fftSize = cheapTrickOption.FftSize;
        int spectrumLength = (fftSize / 2) + 1;
        double[] spectrogram = new double[f0Length * spectrumLength];
        CheapTrick.Estimate(x, fs, cheapTrickOption, positions, f0, spectrogram, Arena);
        double cheapTrick = Lap(watch);

        double[] aperiodicity = new double[f0Length * spectrumLength];
        D4C.Estimate(x, fs, D4COption.Default, positions, f0, fftSize, aperiodicity, Arena);
        double d4c = Lap(watch);

        _x = x;
        _fs = fs;
        _f0 = f0;
        _spectrogram = spectrogram;
        _aperiodicity = aperiodicity;
        _decoded = spectrogram;
        _fftSize = fftSize;
        _spectrumLength = spectrumLength;
        return [harvest, cheapTrick, d4c];
    }

    private static byte[] Render(double[] f0, double[] spectrogram, double[] aperiodicity)
    {
        const string path = "/output.wav";
        double[] y = new double[Synthesis.GetSamplesForSynthesis(_fs, f0.Length, FramePeriod)];
        Synthesis.Synthesize(f0, spectrogram, aperiodicity, _fftSize, FramePeriod, _fs, y, Arena);
        double peak = 0.0;
        foreach (double sample in y)
        {
            peak = Math.Max(peak, Math.Abs(sample));
        }

        if (peak > MaximumPeak)
        {
            double gain = MaximumPeak / peak;
            for (int i = 0; i < y.Length; ++i)
            {
                y[i] *= gain;
            }
        }

        WaveFile.Write(path, y, _fs);
        byte[] wave = File.ReadAllBytes(path);
        File.Delete(path);
        return wave;
    }

    private static double Lap(Stopwatch watch)
    {
        double milliseconds = watch.Elapsed.TotalMilliseconds;
        watch.Restart();
        return milliseconds;
    }

    private static double InterpolateF0(int lower, int upper, double weight)
    {
        double a = _f0[lower];
        double b = _f0[upper];
        if (a > 0.0 && b > 0.0)
        {
            return a + ((b - a) * weight);
        }

        return weight < 0.5 ? a : b;
    }

    private static double Mix(double[] data, int lower, int upper, double weight, double bin)
    {
        double a = Bin(data, lower, bin);
        double b = Bin(data, upper, bin);
        return a + ((b - a) * weight);
    }

    private static double Bin(double[] data, int frame, double bin)
    {
        if (bin >= _spectrumLength - 1)
        {
            return data[((frame + 1) * _spectrumLength) - 1];
        }

        int index = (int)bin;
        double fraction = bin - index;
        int offset = (frame * _spectrumLength) + index;
        return data[offset] + ((data[offset + 1] - data[offset]) * fraction);
    }

    private static double MedianVoiced(double[] f0)
    {
        double[] voiced = new double[f0.Length];
        int count = 0;
        foreach (double value in f0)
        {
            if (value > 0.0)
            {
                voiced[count++] = value;
            }
        }

        if (count == 0)
        {
            return 0.0;
        }

        Array.Sort(voiced, 0, count);
        return voiced[count / 2];
    }
}
