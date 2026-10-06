using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<double> Rotate(Vector256<double> value, Vector256<double> real,
        Vector256<double> imaginary)
    {
        return (value * real) + (SwapRealImaginary(value) * NegateReal(imaginary));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<double> RotateConjugate(Vector256<double> value,
        Vector256<double> real, Vector256<double> imaginary)
    {
        return (value * real) + (SwapRealImaginary(value) * NegateImaginary(imaginary));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Radix4First(double* p0, double* p1, double* p2, double* p3,
        Vector256<double> real1, Vector256<double> imaginary1, Vector256<double> real3,
        Vector256<double> imaginary3, bool backward)
    {
        Vector256<double> v0 = Vector256.Load(p0);
        Vector256<double> v2 = Vector256.Load(p2);
        if (backward)
        {
            v0 = NegateImaginary(v0);
            v2 = NegateImaginary(v2);
        }
        Vector256<double> x0 = v0 + v2;
        Vector256<double> x1 = v0 - v2;
        Vector256<double> v1 = Vector256.Load(p1);
        Vector256<double> v3 = Vector256.Load(p3);
        Vector256<double> x2 = v1 + v3;
        Vector256<double> x3 = v1 - v3;
        Vector256<double> turned = SwapRealImaginary(x3);
        Vector256<double> y2;
        Vector256<double> y3;
        if (backward)
        {
            Vector256<double> flipped = NegateImaginary(x2);
            (x0 + flipped).Store(p0);
            (x0 - flipped).Store(p1);
            y2 = x1 + turned;
            y3 = x1 - turned;
        }
        else
        {
            (x0 + x2).Store(p0);
            (x0 - x2).Store(p1);
            Vector256<double> flipped = NegateReal(turned);
            y2 = x1 + flipped;
            y3 = x1 - flipped;
        }
        Rotate(y2, real1, imaginary1).Store(p2);
        RotateConjugate(y3, real3, imaginary3).Store(p3);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static Vector256<double> CftFirstLoop(int mh, int m, double* a, double* w,
        double csc1, double csc3, bool backward)
    {
        Vector256<double> scale = Vector256.Create(csc1, csc1, csc3, csc3);
        Vector256<double> previous = Vector256.Create(1.0, 0.0, 1.0, 0.0);
        Vector256<double> current = previous;
        int k = 0;
        for (int j = 2; j < mh - 2; j += 4)
        {
            k += 4;
            current = Vector256.Load(w + k);
            Vector256<double> blended = scale * (previous + current);
            previous = current;
            Vector256<double> lower1 = Avx.Permute2x128(blended, current, 0x20);
            Vector256<double> lower3 = Avx.Permute2x128(blended, current, 0x31);
            Vector256<double> real1 = Avx.UnpackLow(lower1, lower1);
            Vector256<double> imaginary1 = Avx.UnpackHigh(lower1, lower1);
            Vector256<double> real3 = Avx.UnpackLow(lower3, lower3);
            Vector256<double> imaginary3 = Avx.UnpackHigh(lower3, lower3);
            Radix4First(a + j, a + j + m, a + j + (2 * m), a + j + (3 * m), real1, imaginary1,
                real3, imaginary3, backward);
            int j0 = m - j - 2;
            Radix4First(a + j0, a + j0 + m, a + j0 + (2 * m), a + j0 + (3 * m),
                SwapHalves(imaginary1), SwapHalves(real1), SwapHalves(imaginary3),
                SwapHalves(real3), backward);
        }
        return current;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<double> LoadPair(double* low, double* high)
    {
        return Vector256.Create(Vector128.Load(low), Vector128.Load(high));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StorePair(Vector256<double> value, double* low, double* high)
    {
        value.GetLower().Store(low);
        value.GetUpper().Store(high);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Radix4Pair(double* low, double* high, int m, Vector256<double> real1,
        Vector256<double> imaginary1, Vector256<double> real3, Vector256<double> imaginary3)
    {
        Vector256<double> v0 = LoadPair(low, high);
        Vector256<double> v2 = LoadPair(low + (2 * m), high + (2 * m));
        Vector256<double> x0 = v0 + v2;
        Vector256<double> x1 = v0 - v2;
        Vector256<double> v1 = LoadPair(low + m, high + m);
        Vector256<double> v3 = LoadPair(low + (3 * m), high + (3 * m));
        Vector256<double> x2 = v1 + v3;
        Vector256<double> x3 = v1 - v3;
        StorePair(x0 + x2, low, high);
        StorePair(x0 - x2, low + m, high + m);
        Vector256<double> flipped = NegateReal(SwapRealImaginary(x3));
        Vector256<double> y2 = x1 + flipped;
        Vector256<double> y3 = x1 - flipped;
        StorePair(Rotate(y2, real1, imaginary1), low + (2 * m), high + (2 * m));
        StorePair(RotateConjugate(y3, real3, imaginary3), low + (3 * m), high + (3 * m));
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void CftMiddleLoop1(int mh, int m, double* a, double* w)
    {
        int k = 0;
        for (int j = 2; j < mh; j += 2)
        {
            k += 4;
            Vector256<double> table = Vector256.Load(w + k);
            Radix4Pair(a + j, a + m - j, m, Avx2.Permute4x64(table, 0x50),
                Avx2.Permute4x64(table, 0x05), Avx2.Permute4x64(table, 0xFA),
                Avx2.Permute4x64(table, 0xAF));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void CftMiddleLoop2(int mh, int m, double* a, double* w)
    {
        int k = 0;
        int kr = 2 * m;
        for (int j = 2; j < mh; j += 2)
        {
            k += 4;
            kr -= 4;
            Vector256<double> forwardTable = Vector256.Load(w + k);
            Vector256<double> reverseTable = Vector256.Load(w + kr);
            Vector256<double> first = Avx.Permute2x128(forwardTable, reverseTable, 0x20);
            Vector256<double> second = Avx.Permute2x128(forwardTable, reverseTable, 0x31);
            Vector256<double> real0 = Avx.UnpackLow(first, first);
            Vector256<double> imaginary0 = Avx.UnpackHigh(first, first);
            Vector256<double> real1 = Avx.UnpackLow(second, second);
            Vector256<double> imaginary1 = Avx.UnpackHigh(second, second);
            double* low = a + j;
            double* high = a + m - j;
            Vector256<double> u = LoadPair(low, high);
            Vector256<double> v = LoadPair(low + (2 * m), high + (2 * m));
            Vector256<double> s = LoadPair(low + m, high + m);
            Vector256<double> t = LoadPair(low + (3 * m), high + (3 * m));
            Vector256<double> turnedV = NegateReal(SwapRealImaginary(v));
            Vector256<double> turnedT = NegateReal(SwapRealImaginary(t));
            Vector256<double> y0 = Rotate(u + turnedV, real0, imaginary0);
            Vector256<double> y2 = Rotate(s + turnedT, SwapHalves(imaginary0), SwapHalves(real0));
            Vector256<double> z0 = RotateConjugate(u - turnedV, real1, imaginary1);
            Vector256<double> z2 =
                RotateConjugate(s - turnedT, SwapHalves(imaginary1), SwapHalves(real1));
            StorePair(y0 + y2, low, high);
            StorePair(y0 - y2, low + m, high + m);
            StorePair(z0 + z2, low + (2 * m), high + (2 * m));
            StorePair(z0 - z2, low + (3 * m), high + (3 * m));
        }
    }
}
