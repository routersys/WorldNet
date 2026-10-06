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
    double best = double.MaxValue; double[] bestp = new double[32];
    for (int r = 0; r < 30; ++r)
    {
        Array.Clear(Prof.Acc);
        long a0 = Stopwatch.GetTimestamp();
        CheapTrick.Estimate(x, fs, ct, positions, refined, sp, arena);
        double ms = (Stopwatch.GetTimestamp() - a0) * 1e3 / Stopwatch.Frequency;
        if (ms < best) { best = ms; Array.Copy(Prof.Acc, bestp, 32); }
    }
    Console.WriteLine($"CheapTrick total {best:F3} ms (frames {f0Length})");
    string[] names = { "setparams(cos,window)", "windowed(randn)+sums", "fft(power)", "power spectrum", "linear smoothing", "infinitesimal(randn)", "lifters(sin/cos)", "log loop", "fft fwd (cepstrum)", "lifter mult", "ifft", "exp loop", "", "dc correction" };
    for (int i = 0; i < 14; ++i) if (names[i].Length > 0) Console.WriteLine($"  {names[i],-22} {bestp[i] / 1000.0,8:F3} ms");
}
