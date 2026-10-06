using System.Diagnostics;
namespace WorldNet;
public static class Prof
{
    public static long Count;
    public static System.Collections.Generic.List<double[]> Captured = new();
    public static double[] Acc = new double[32];
    public static void PT(int i, ref long t)
    {
        long n = Stopwatch.GetTimestamp();
        Acc[i] += (n - t) * (1e6 / Stopwatch.Frequency);
        t = n;
    }
}
