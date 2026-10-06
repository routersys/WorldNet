using System.Diagnostics;
using WorldNet;

unsafe
{
    int length = WaveFile.GetLength(args[0]);
    double[] x = new double[length];
    WaveFile.Read(args[0], x, out int fs, out _);
    const double framePeriod = 5.0;
    using WorldArena arena = new();
    DioOption dioOption = DioOption.Default with { FramePeriod = framePeriod };
    int f0Length = Dio.GetSamplesForDio(fs, x.Length, framePeriod);
    double[] positions = new double[f0Length], f0 = new double[f0Length], refined = new double[f0Length];
    Dio.Estimate(x, fs, dioOption, positions, f0, arena);
    StoneMask.Refine(x, fs, positions, f0, refined, arena);
    CheapTrickOption ct = CheapTrickOption.Create(fs);
    int fftSize = ct.FftSize, sl = fftSize / 2 + 1;
    double[] sp = new double[f0Length * sl], ap = new double[f0Length * sl];
    CheapTrick.Estimate(x, fs, ct, positions, refined, sp, arena);
    D4C.Estimate(x, fs, D4COption.Default, positions, refined, fftSize, ap, arena);
    int yLength = Synthesis.GetSamplesForSynthesis(fs, f0Length, framePeriod);
    double[] y = new double[yLength];
    double best = double.MaxValue; double[] bestp = new double[32];
    for (int r = 0; r < 30; ++r)
    {
        Array.Clear(Prof.Acc);
        long a = Stopwatch.GetTimestamp();
        Synthesis.Synthesize(refined, sp, ap, fftSize, framePeriod, fs, y, arena);
        double ms = (Stopwatch.GetTimestamp() - a) * 1e3 / Stopwatch.Frequency;
        if (ms < best) { best = ms; Array.Copy(Prof.Acc, bestp, 32); }
    }
    Console.WriteLine($"Synthesis total {best:F3} ms");
    string[] names = { "env+ratio", "per.log", "per.minphase", "per.shiftloop", "per.ifft+shift+dc", "ap.noise(randn+fft)", "ap.log", "ap.minphase", "ap.mult", "ap.ifft+shift", "final mix", "accumulate", "", "", "mp.mirror", "mp.ifft(r2c)", "mp.cepstrum", "mp.fft(c2c)", "mp.exp/sincos" };
    for (int i = 0; i < 19; ++i) if (names[i].Length > 0) Console.WriteLine($"  {names[i],-22} {bestp[i] / 1000.0,8:F3} ms");
}
