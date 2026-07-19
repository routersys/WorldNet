using System.Diagnostics;
using WorldNet;

if (args.Length == 0)
{
    return Usage();
}

try
{
    return args[0] switch
    {
        "f0" => AnalyzeF0(args),
        "spectrum" => AnalyzeSpectrum(args),
        "aperiodicity" => AnalyzeAperiodicity(args),
        "synthesize" => SynthesizeFromFiles(args),
        "pipeline" => RunPipeline(args),
        _ => Usage(),
    };
}
catch (Exception exception)
{
    Console.Error.WriteLine($"error: {exception.Message}");
    return 1;
}

static int Usage()
{
    Console.Error.WriteLine("usage:");
    Console.Error.WriteLine("  f0           <input.wav> <output.f0> [--text]");
    Console.Error.WriteLine("  spectrum     <input.wav> <input.f0> <output.sp> [dimensions]");
    Console.Error.WriteLine("  aperiodicity <input.wav> <input.f0> <output.ap> [dimensions]");
    Console.Error.WriteLine("  synthesize   <input.f0> <input.sp> <input.ap> <output.wav>");
    Console.Error.WriteLine("  pipeline     <input.wav> <output.wav>");
    return 1;
}

static double[] ReadWave(string path, out int fs)
{
    int length = WaveFile.GetLength(path);
    double[] x = new double[length];
    WaveFile.Read(path, x, out fs, out _);
    return x;
}

static int AnalyzeF0(string[] args)
{
    if (args.Length < 3)
    {
        return Usage();
    }

    double[] x = ReadWave(args[1], out int fs);
    bool asText = args.Contains("--text");

    using WorldArena arena = new();
    HarvestOption option = HarvestOption.Default;
    int f0Length = Harvest.GetSamplesForHarvest(fs, x.Length, option.FramePeriod);
    double[] temporalPositions = new double[f0Length];
    double[] f0 = new double[f0Length];
    Harvest.Estimate(x, fs, option, temporalPositions, f0, arena);

    ParameterFile.WriteF0(args[2], option.FramePeriod, temporalPositions, f0, asText);
    Console.WriteLine($"wrote {f0Length} frames to {args[2]}");
    return 0;
}

static int AnalyzeSpectrum(string[] args)
{
    if (args.Length < 4)
    {
        return Usage();
    }

    double[] x = ReadWave(args[1], out int fs);
    int f0Length = (int)ParameterFile.GetHeaderInformation(args[2], "NOF ");
    double framePeriod = ParameterFile.GetHeaderInformation(args[2], "FP  ");
    double[] positions = new double[f0Length];
    double[] f0 = new double[f0Length];
    ParameterFile.ReadF0(args[2], positions, f0);
    int dimensions = args.Length > 4 ? int.Parse(args[4]) : 0;

    using WorldArena arena = new();
    CheapTrickOption option = CheapTrickOption.Create(fs);
    int fftSize = option.FftSize;
    int spectrumLength = (fftSize / 2) + 1;
    double[] spectrogram = new double[f0Length * spectrumLength];
    CheapTrick.Estimate(x, fs, option, positions, f0, spectrogram, arena);

    if (dimensions > 0)
    {
        double[] coded = new double[f0Length * dimensions];
        Codec.CodeSpectralEnvelope(spectrogram, f0Length, fs, fftSize, dimensions, coded, arena);
        ParameterFile.WriteSpectralEnvelope(args[3], fs, f0Length, framePeriod, fftSize,
            dimensions, coded);
    }
    else
    {
        ParameterFile.WriteSpectralEnvelope(args[3], fs, f0Length, framePeriod, fftSize, 0,
            spectrogram);
    }

    Console.WriteLine($"wrote {f0Length} frames to {args[3]}");
    return 0;
}

static int AnalyzeAperiodicity(string[] args)
{
    if (args.Length < 4)
    {
        return Usage();
    }

    double[] x = ReadWave(args[1], out int fs);
    int f0Length = (int)ParameterFile.GetHeaderInformation(args[2], "NOF ");
    double framePeriod = ParameterFile.GetHeaderInformation(args[2], "FP  ");
    double[] positions = new double[f0Length];
    double[] f0 = new double[f0Length];
    ParameterFile.ReadF0(args[2], positions, f0);
    int dimensions = args.Length > 4 ? int.Parse(args[4]) : 0;

    using WorldArena arena = new();
    int fftSize = CheapTrickOption.Create(fs).FftSize;
    int spectrumLength = (fftSize / 2) + 1;
    double[] aperiodicity = new double[f0Length * spectrumLength];
    D4C.Estimate(x, fs, D4COption.Default, positions, f0, fftSize, aperiodicity, arena);

    if (dimensions > 0)
    {
        int coefficients = Codec.GetNumberOfAperiodicities(fs);
        double[] coded = new double[f0Length * coefficients];
        Codec.CodeAperiodicity(aperiodicity, f0Length, fs, fftSize, coded, arena);
        ParameterFile.WriteAperiodicity(args[3], fs, f0Length, framePeriod, fftSize,
            coefficients, coded);
    }
    else
    {
        ParameterFile.WriteAperiodicity(args[3], fs, f0Length, framePeriod, fftSize, 0,
            aperiodicity);
    }

    Console.WriteLine($"wrote {f0Length} frames to {args[3]}");
    return 0;
}

static int SynthesizeFromFiles(string[] args)
{
    if (args.Length < 5)
    {
        return Usage();
    }

    int f0Length = (int)ParameterFile.GetHeaderInformation(args[1], "NOF ");
    int fftSize = (int)ParameterFile.GetHeaderInformation(args[2], "FFT ");
    int fs = (int)ParameterFile.GetHeaderInformation(args[2], "FS  ");
    double framePeriod = ParameterFile.GetHeaderInformation(args[2], "FP  ");
    int spectrumLength = (fftSize / 2) + 1;

    double[] positions = new double[f0Length];
    double[] f0 = new double[f0Length];
    ParameterFile.ReadF0(args[1], positions, f0);

    using WorldArena arena = new();
    double[] spectrogram = ReadSpectrum(args[2], f0Length, fftSize, spectrumLength, fs, arena);
    double[] aperiodicity =
        ReadAperiodicity(args[3], f0Length, fftSize, spectrumLength, fs, arena);

    int yLength = (int)(f0Length * framePeriod / 1000.0 * fs);
    double[] y = new double[yLength];
    Synthesis.Synthesize(f0, spectrogram, aperiodicity, fftSize, framePeriod, fs, y, arena);

    WaveFile.Write(args[4], y, fs);
    Console.WriteLine($"wrote {yLength} samples to {args[4]}");
    return 0;
}

static double[] ReadSpectrum(string path, int f0Length, int fftSize, int spectrumLength, int fs,
    WorldArena arena)
{
    int stored = (int)ParameterFile.GetHeaderInformation(path, "NOD ");
    if (stored <= 0 || stored == spectrumLength)
    {
        double[] raw = new double[f0Length * spectrumLength];
        ParameterFile.ReadSpectralEnvelope(path, raw, out _, out _);
        return raw;
    }

    double[] coded = new double[f0Length * stored];
    ParameterFile.ReadSpectralEnvelope(path, coded, out _, out _);
    double[] decoded = new double[f0Length * spectrumLength];
    Codec.DecodeSpectralEnvelope(coded, f0Length, fs, fftSize, stored, decoded, arena);
    return decoded;
}

static double[] ReadAperiodicity(string path, int f0Length, int fftSize, int spectrumLength,
    int fs, WorldArena arena)
{
    int stored = (int)ParameterFile.GetHeaderInformation(path, "NOD ");
    if (stored <= 0 || stored == spectrumLength)
    {
        double[] raw = new double[f0Length * spectrumLength];
        ParameterFile.ReadAperiodicity(path, raw, out _, out _);
        return raw;
    }

    double[] coded = new double[f0Length * stored];
    ParameterFile.ReadAperiodicity(path, coded, out _, out _);
    double[] decoded = new double[f0Length * spectrumLength];
    Codec.DecodeAperiodicity(coded, f0Length, fs, fftSize, decoded, arena);
    return decoded;
}

static int RunPipeline(string[] args)
{
    if (args.Length < 3)
    {
        return Usage();
    }

    double[] x = ReadWave(args[1], out int fs);
    Console.WriteLine($"input: {x.Length} samples, {fs} Hz");

    using WorldArena arena = new();
    Stopwatch stopwatch = Stopwatch.StartNew();

    DioOption dioOption = DioOption.Default;
    int f0Length = Dio.GetSamplesForDio(fs, x.Length, dioOption.FramePeriod);
    double[] temporalPositions = new double[f0Length];
    double[] f0 = new double[f0Length];
    Dio.Estimate(x, fs, dioOption, temporalPositions, f0, arena);
    Console.WriteLine($"Dio: {stopwatch.ElapsedMilliseconds} ms");

    stopwatch.Restart();
    double[] refinedF0 = new double[f0Length];
    StoneMask.Refine(x, fs, temporalPositions, f0, refinedF0, arena);
    Console.WriteLine($"StoneMask: {stopwatch.ElapsedMilliseconds} ms");

    stopwatch.Restart();
    CheapTrickOption cheapTrickOption = CheapTrickOption.Create(fs);
    int fftSize = cheapTrickOption.FftSize;
    int spectrumLength = (fftSize / 2) + 1;
    double[] spectrogram = new double[f0Length * spectrumLength];
    CheapTrick.Estimate(x, fs, cheapTrickOption, temporalPositions, refinedF0, spectrogram,
        arena);
    Console.WriteLine($"CheapTrick: {stopwatch.ElapsedMilliseconds} ms");

    stopwatch.Restart();
    double[] aperiodicity = new double[f0Length * spectrumLength];
    D4C.Estimate(x, fs, D4COption.Default, temporalPositions, refinedF0, fftSize, aperiodicity,
        arena);
    Console.WriteLine($"D4C: {stopwatch.ElapsedMilliseconds} ms");

    stopwatch.Restart();
    int yLength = (int)((f0Length - 1) * dioOption.FramePeriod / 1000.0 * fs) + 1;
    double[] y = new double[yLength];
    Synthesis.Synthesize(refinedF0, spectrogram, aperiodicity, fftSize, dioOption.FramePeriod,
        fs, y, arena);
    Console.WriteLine($"Synthesis: {stopwatch.ElapsedMilliseconds} ms");

    WaveFile.Write(args[2], y, fs);
    Console.WriteLine($"wrote {yLength} samples to {args[2]}");
    Console.WriteLine($"arena: {arena.Used} of {arena.Capacity} bytes");
    return 0;
}
