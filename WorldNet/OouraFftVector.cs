using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace WorldNet;

internal static unsafe partial class OouraFft
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<double> SwapHalves(Vector256<double> value)
    {
        return Vector256.Shuffle(value, Vector256.Create(2L, 3L, 0L, 1L));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<double> SwapRealImaginary(Vector256<double> value)
    {
        return Vector256.Shuffle(value, Vector256.Create(1L, 0L, 3L, 2L));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<double> NegateReal(Vector256<double> value)
    {
        return value ^ Vector256.Create(-0.0, 0.0, -0.0, 0.0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<double> NegateImaginary(Vector256<double> value)
    {
        return value ^ Vector256.Create(0.0, -0.0, 0.0, -0.0);
    }
}
