using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace WorldNet;

internal static unsafe partial class OouraFft
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<double> SwapRealImaginary(Vector128<double> value)
    {
        return Vector128.Shuffle(value, Vector128.Create(1L, 0L));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<double> NegateReal(Vector128<double> value)
    {
        return value ^ Vector128.Create(-0.0, 0.0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<double> NegateImaginary(Vector128<double> value)
    {
        return value ^ Vector128.Create(0.0, -0.0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<double> Turn(Vector128<double> value)
    {
        return NegateReal(SwapRealImaginary(value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<double> Rotate(Vector128<double> value, Vector128<double> real,
        Vector128<double> imaginary)
    {
        return (value * real) + (SwapRealImaginary(value) * NegateReal(imaginary));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<double> RotateConjugate(Vector128<double> value,
        Vector128<double> real, Vector128<double> imaginary)
    {
        return (value * real) + (SwapRealImaginary(value) * NegateImaginary(imaginary));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Radix4Narrow(double* p0, double* p1, double* p2, double* p3, double wr1,
        double wi1, double wr3, double wi3, bool backward)
    {
        Vector128<double> v0 = Vector128.Load(p0);
        Vector128<double> v2 = Vector128.Load(p2);
        if (backward)
        {
            v0 = NegateImaginary(v0);
            v2 = NegateImaginary(v2);
        }
        Vector128<double> x0 = v0 + v2;
        Vector128<double> x1 = v0 - v2;
        Vector128<double> v1 = Vector128.Load(p1);
        Vector128<double> v3 = Vector128.Load(p3);
        Vector128<double> x2 = v1 + v3;
        Vector128<double> x3 = v1 - v3;
        Vector128<double> turned = SwapRealImaginary(x3);
        Vector128<double> y2;
        Vector128<double> y3;
        if (backward)
        {
            Vector128<double> flipped = NegateImaginary(x2);
            (x0 + flipped).Store(p0);
            (x0 - flipped).Store(p1);
            y2 = x1 + turned;
            y3 = x1 - turned;
        }
        else
        {
            (x0 + x2).Store(p0);
            (x0 - x2).Store(p1);
            Vector128<double> flipped = NegateReal(turned);
            y2 = x1 + flipped;
            y3 = x1 - flipped;
        }
        Rotate(y2, Vector128.Create(wr1), Vector128.Create(wi1)).Store(p2);
        RotateConjugate(y3, Vector128.Create(wr3), Vector128.Create(wi3)).Store(p3);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void CftFirstLoopNarrow(int mh, int m, double* a, double* w, double csc1,
        double csc3, bool backward, out double wd1r, out double wd1i, out double wd3r,
        out double wd3i)
    {
        wd1r = 1;
        wd1i = 0;
        wd3r = 1;
        wd3i = 0;
        int k = 0;
        for (int j = 2; j < mh - 2; j += 4)
        {
            k += 4;
            double wk1r = csc1 * (wd1r + w[k]);
            double wk1i = csc1 * (wd1i + w[k + 1]);
            double wk3r = csc3 * (wd3r + w[k + 2]);
            double wk3i = csc3 * (wd3i + w[k + 3]);
            wd1r = w[k];
            wd1i = w[k + 1];
            wd3r = w[k + 2];
            wd3i = w[k + 3];
            Radix4Narrow(a + j, a + j + m, a + j + (2 * m), a + j + (3 * m), wk1r, wk1i, wk3r,
                wk3i, backward);
            Radix4Narrow(a + j + 2, a + j + 2 + m, a + j + 2 + (2 * m), a + j + 2 + (3 * m),
                wd1r, wd1i, wd3r, wd3i, backward);
            int j0 = m - j;
            Radix4Narrow(a + j0, a + j0 + m, a + j0 + (2 * m), a + j0 + (3 * m), wk1i, wk1r,
                wk3i, wk3r, backward);
            Radix4Narrow(a + j0 - 2, a + j0 - 2 + m, a + j0 - 2 + (2 * m),
                a + j0 - 2 + (3 * m), wd1i, wd1r, wd3i, wd3r, backward);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void CftMiddleLoop1Narrow(int mh, int m, double* a, double* w)
    {
        int k = 0;
        for (int j = 2; j < mh; j += 2)
        {
            k += 4;
            double wk1r = w[k];
            double wk1i = w[k + 1];
            double wk3r = w[k + 2];
            double wk3i = w[k + 3];
            double* low = a + j;
            Radix4Narrow(low, low + m, low + (2 * m), low + (3 * m), wk1r, wk1i, wk3r, wk3i,
                false);
            double* high = a + m - j;
            Radix4Narrow(high, high + m, high + (2 * m), high + (3 * m), wk1i, wk1r, wk3i,
                wk3r, false);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Radix4Mdl2Narrow(double* p, int m, double r0, double i0, double r2,
        double i2, double r1, double i1, double r3, double i3)
    {
        Vector128<double> u = Vector128.Load(p);
        Vector128<double> v = Vector128.Load(p + (2 * m));
        Vector128<double> s = Vector128.Load(p + m);
        Vector128<double> t = Vector128.Load(p + (3 * m));
        Vector128<double> turnedV = Turn(v);
        Vector128<double> turnedT = Turn(t);
        Vector128<double> y0 = Rotate(u + turnedV, Vector128.Create(r0), Vector128.Create(i0));
        Vector128<double> y2 = Rotate(s + turnedT, Vector128.Create(r2), Vector128.Create(i2));
        Vector128<double> z0 =
            RotateConjugate(u - turnedV, Vector128.Create(r1), Vector128.Create(i1));
        Vector128<double> z2 =
            RotateConjugate(s - turnedT, Vector128.Create(r3), Vector128.Create(i3));
        (y0 + y2).Store(p);
        (y0 - y2).Store(p + m);
        (z0 + z2).Store(p + (2 * m));
        (z0 - z2).Store(p + (3 * m));
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void CftMiddleLoop2Narrow(int mh, int m, double* a, double* w)
    {
        int k = 0;
        int kr = 2 * m;
        for (int j = 2; j < mh; j += 2)
        {
            k += 4;
            kr -= 4;
            double wk1r = w[k];
            double wk1i = w[k + 1];
            double wk3r = w[k + 2];
            double wk3i = w[k + 3];
            double wd1i = w[kr];
            double wd1r = w[kr + 1];
            double wd3i = w[kr + 2];
            double wd3r = w[kr + 3];
            Radix4Mdl2Narrow(a + j, m, wk1r, wk1i, wd1r, wd1i, wk3r, wk3i, wd3r, wd3i);
            Radix4Mdl2Narrow(a + m - j, m, wd1i, wd1r, wk1i, wk1r, wd3i, wd3r, wk3i, wk3r);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void RftLoopNarrow(int n, double* a, int nc, double* c, int m, int ks,
        ref int kk, bool backward)
    {
        for (int j = 2; j < m; j += 2)
        {
            int k = n - j;
            kk += ks;
            Vector128<double> wkr = Vector128.Create(0.5 - c[nc - kk]);
            Vector128<double> wki = Vector128.Create(c[kk]);
            Vector128<double> upper = Vector128.Load(a + j);
            Vector128<double> lower = Vector128.Load(a + k);
            Vector128<double> x = upper + NegateReal(lower);
            Vector128<double> signed = backward ? NegateImaginary(wki) : NegateReal(wki);
            Vector128<double> y = (x * wkr) + (SwapRealImaginary(x) * signed);
            (upper - y).Store(a + j);
            (lower + NegateImaginary(y)).Store(a + k);
        }
    }
}
