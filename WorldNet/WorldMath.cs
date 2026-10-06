using System.Runtime.CompilerServices;

namespace WorldNet;

internal static class WorldMath
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int MaxInt(int x, int y)
    {
        return x > y ? x : y;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double MaxDouble(double x, double y)
    {
        return x > y ? x : y;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int MinInt(int x, int y)
    {
        return x < y ? x : y;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double MinDouble(double x, double y)
    {
        return x < y ? x : y;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double GetSafeAperiodicity(double x)
    {
        return MaxDouble(0.001, MinDouble(0.999999999999, x));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (double Sine, double Cosine) SinCos(double x)
    {
#if NET10_0_OR_GREATER
        return Math.SinCos(x);
#else
        return (Math.Sin(x), Math.Cos(x));
#endif
    }
}
