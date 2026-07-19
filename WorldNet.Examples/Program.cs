using System.Diagnostics;
using WorldNet;

if (args.Length < 2)
{
    Console.Error.WriteLine("usage: WorldNet.Examples <input.wav> <output.wav>");
    return 1;
}

string inputPath = args[0];
string outputPath = args[1];

int xLength = WaveFile.GetLength(inputPath);
if (xLength <= 0)
{
    Console.Error.WriteLine($"cannot read {inputPath}");
    return 1;
}

double[] x = new double[xLength];
WaveFile.Read(inputPath, x, out int fs, out int bitDepth);
Console.WriteLine($"input: {xLength} samples, {fs} Hz, {bitDepth} bit");

using WorldArena arena = new();
Stopwatch stopwatch = Stopwatch.StartNew();

DioOption dioOption = DioOption.Default;
int f0Length = Dio.GetSamplesForDio(fs, xLength, dioOption.FramePeriod);
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
CheapTrick.Estimate(x, fs, cheapTrickOption, temporalPositions, refinedF0, spectrogram, arena);
Console.WriteLine($"CheapTrick: {stopwatch.ElapsedMilliseconds} ms");

stopwatch.Restart();
double[] aperiodicity = new double[f0Length * spectrumLength];
D4C.Estimate(x, fs, D4COption.Default, temporalPositions, refinedF0, fftSize, aperiodicity,
    arena);
Console.WriteLine($"D4C: {stopwatch.ElapsedMilliseconds} ms");

stopwatch.Restart();
int yLength = (int)((f0Length - 1) * dioOption.FramePeriod / 1000.0 * fs) + 1;
double[] y = new double[yLength];
Synthesis.Synthesize(refinedF0, spectrogram, aperiodicity, fftSize, dioOption.FramePeriod, fs, y,
    arena);
Console.WriteLine($"Synthesis: {stopwatch.ElapsedMilliseconds} ms");

WaveFile.Write(outputPath, y, fs);
Console.WriteLine($"wrote {yLength} samples to {outputPath}");
Console.WriteLine($"arena: {arena.Used} of {arena.Capacity} bytes");
return 0;
