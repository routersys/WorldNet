using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace WorldNet;

internal static unsafe partial class OouraFft
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<double> Turn(Vector256<double> value)
    {
        return NegateReal(SwapRealImaginary(value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<double> LoadPoint(double* a0, double* a1, int index)
    {
        return LoadPair(a0 + (2 * index), a1 + (2 * index));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StorePoint(Vector256<double> value, double* a0, double* a1, int index)
    {
        StorePair(value, a0 + (2 * index), a1 + (2 * index));
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void CftF081Pair(double* a0, double* a1, double* w)
    {
        Vector256<double> wn4r = Vector256.Create(w[1]);
        Vector256<double> c0 = LoadPoint(a0, a1, 0);
        Vector256<double> c4 = LoadPoint(a0, a1, 4);
        Vector256<double> c2 = LoadPoint(a0, a1, 2);
        Vector256<double> c6 = LoadPoint(a0, a1, 6);
        Vector256<double> x0 = c0 + c4;
        Vector256<double> x1 = c0 - c4;
        Vector256<double> x2 = c2 + c6;
        Vector256<double> x3 = c2 - c6;
        Vector256<double> y0 = x0 + x2;
        Vector256<double> y2 = x0 - x2;
        Vector256<double> y1 = x1 + Turn(x3);
        Vector256<double> y3 = x1 - Turn(x3);
        Vector256<double> c1 = LoadPoint(a0, a1, 1);
        Vector256<double> c5 = LoadPoint(a0, a1, 5);
        Vector256<double> c3 = LoadPoint(a0, a1, 3);
        Vector256<double> c7 = LoadPoint(a0, a1, 7);
        x0 = c1 + c5;
        x1 = c1 - c5;
        x2 = c3 + c7;
        x3 = c3 - c7;
        Vector256<double> y4 = x0 + x2;
        Vector256<double> y6 = x0 - x2;
        x0 = x1 + Turn(x3);
        x2 = x1 - Turn(x3);
        Vector256<double> y5 = wn4r * (x0 + Turn(x0));
        Vector256<double> y7 = wn4r * (x2 + Turn(x2));
        StorePoint(y1 + y5, a0, a1, 4);
        StorePoint(y1 - y5, a0, a1, 5);
        StorePoint(y3 + Turn(y7), a0, a1, 6);
        StorePoint(y3 - Turn(y7), a0, a1, 7);
        StorePoint(y0 + y4, a0, a1, 0);
        StorePoint(y0 - y4, a0, a1, 1);
        StorePoint(y2 + Turn(y6), a0, a1, 2);
        StorePoint(y2 - Turn(y6), a0, a1, 3);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void CftF082Pair(double* a0, double* a1, double* w)
    {
        Vector256<double> wn4r = Vector256.Create(w[1]);
        Vector256<double> wk1r = Vector256.Create(w[2]);
        Vector256<double> wk1i = Vector256.Create(w[3]);
        Vector256<double> c0 = LoadPoint(a0, a1, 0);
        Vector256<double> c4 = LoadPoint(a0, a1, 4);
        Vector256<double> y0 = c0 + Turn(c4);
        Vector256<double> y1 = c0 - Turn(c4);
        Vector256<double> c2 = LoadPoint(a0, a1, 2);
        Vector256<double> c6 = LoadPoint(a0, a1, 6);
        Vector256<double> x0 = c2 + Turn(c6);
        Vector256<double> y2 = wn4r * (x0 + Turn(x0));
        x0 = c2 - Turn(c6);
        Vector256<double> y3 = wn4r * (x0 + Turn(x0));
        Vector256<double> c1 = LoadPoint(a0, a1, 1);
        Vector256<double> c5 = LoadPoint(a0, a1, 5);
        Vector256<double> y4 = Rotate(c1 + Turn(c5), wk1r, wk1i);
        Vector256<double> y5 = Rotate(c1 - Turn(c5), wk1i, wk1r);
        Vector256<double> c3 = LoadPoint(a0, a1, 3);
        Vector256<double> c7 = LoadPoint(a0, a1, 7);
        Vector256<double> y6 = Rotate(c3 + Turn(c7), wk1i, wk1r);
        Vector256<double> y7 = Rotate(c3 - Turn(c7), wk1r, wk1i);
        x0 = y0 + y2;
        Vector256<double> x1 = y4 + y6;
        StorePoint(x0 + x1, a0, a1, 0);
        StorePoint(x0 - x1, a0, a1, 1);
        x0 = y0 - y2;
        x1 = y4 - y6;
        StorePoint(x0 + Turn(x1), a0, a1, 2);
        StorePoint(x0 - Turn(x1), a0, a1, 3);
        x0 = y1 + Turn(y3);
        x1 = y5 - y7;
        StorePoint(x0 + x1, a0, a1, 4);
        StorePoint(x0 - x1, a0, a1, 5);
        x0 = y1 - Turn(y3);
        x1 = y5 + y7;
        StorePoint(x0 + Turn(x1), a0, a1, 6);
        StorePoint(x0 - Turn(x1), a0, a1, 7);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void CftF161Pair(double* a0, double* a1, double* w)
    {
        Vector256<double> wn4r = Vector256.Create(w[1]);
        Vector256<double> wk1r = Vector256.Create(w[2]);
        Vector256<double> wk1i = Vector256.Create(w[3]);
        Vector256<double> x0 = LoadPoint(a0, a1, 0) + LoadPoint(a0, a1, 8);
        Vector256<double> x1 = LoadPoint(a0, a1, 0) - LoadPoint(a0, a1, 8);
        Vector256<double> x2 = LoadPoint(a0, a1, 4) + LoadPoint(a0, a1, 12);
        Vector256<double> x3 = LoadPoint(a0, a1, 4) - LoadPoint(a0, a1, 12);
        Vector256<double> y0 = x0 + x2;
        Vector256<double> y4 = x0 - x2;
        Vector256<double> y8 = x1 + Turn(x3);
        Vector256<double> y12 = x1 - Turn(x3);
        x0 = LoadPoint(a0, a1, 1) + LoadPoint(a0, a1, 9);
        x1 = LoadPoint(a0, a1, 1) - LoadPoint(a0, a1, 9);
        x2 = LoadPoint(a0, a1, 5) + LoadPoint(a0, a1, 13);
        x3 = LoadPoint(a0, a1, 5) - LoadPoint(a0, a1, 13);
        Vector256<double> y1 = x0 + x2;
        Vector256<double> y5 = x0 - x2;
        Vector256<double> y9 = Rotate(x1 + Turn(x3), wk1r, wk1i);
        Vector256<double> y13 = Rotate(x1 - Turn(x3), wk1i, wk1r);
        x0 = LoadPoint(a0, a1, 2) + LoadPoint(a0, a1, 10);
        x1 = LoadPoint(a0, a1, 2) - LoadPoint(a0, a1, 10);
        x2 = LoadPoint(a0, a1, 6) + LoadPoint(a0, a1, 14);
        x3 = LoadPoint(a0, a1, 6) - LoadPoint(a0, a1, 14);
        Vector256<double> y2 = x0 + x2;
        Vector256<double> y6 = x0 - x2;
        x0 = x1 + Turn(x3);
        Vector256<double> y10 = wn4r * (x0 + Turn(x0));
        x0 = x1 - Turn(x3);
        Vector256<double> y14 = wn4r * (x0 - Turn(x0));
        x0 = LoadPoint(a0, a1, 3) + LoadPoint(a0, a1, 11);
        x1 = LoadPoint(a0, a1, 3) - LoadPoint(a0, a1, 11);
        x2 = LoadPoint(a0, a1, 7) + LoadPoint(a0, a1, 15);
        x3 = LoadPoint(a0, a1, 7) - LoadPoint(a0, a1, 15);
        Vector256<double> y3 = x0 + x2;
        Vector256<double> y7 = x0 - x2;
        Vector256<double> y11 = Rotate(x1 + Turn(x3), wk1i, wk1r);
        Vector256<double> y15 = Rotate(x1 - Turn(x3), wk1r, wk1i);
        x0 = y12 - y14;
        x1 = y12 + y14;
        x2 = y13 - y15;
        x3 = y13 + y15;
        StorePoint(x0 + x2, a0, a1, 12);
        StorePoint(x0 - x2, a0, a1, 13);
        StorePoint(x1 + Turn(x3), a0, a1, 14);
        StorePoint(x1 - Turn(x3), a0, a1, 15);
        x0 = y8 + y10;
        x1 = y8 - y10;
        x2 = y9 + y11;
        x3 = y9 - y11;
        StorePoint(x0 + x2, a0, a1, 8);
        StorePoint(x0 - x2, a0, a1, 9);
        StorePoint(x1 + Turn(x3), a0, a1, 10);
        StorePoint(x1 - Turn(x3), a0, a1, 11);
        x0 = y5 + Turn(y7);
        x2 = wn4r * (x0 + Turn(x0));
        x0 = y5 - Turn(y7);
        x3 = wn4r * (x0 + Turn(x0));
        x0 = y4 + Turn(y6);
        x1 = y4 - Turn(y6);
        StorePoint(x0 + x2, a0, a1, 4);
        StorePoint(x0 - x2, a0, a1, 5);
        StorePoint(x1 + Turn(x3), a0, a1, 6);
        StorePoint(x1 - Turn(x3), a0, a1, 7);
        x0 = y0 + y2;
        x1 = y0 - y2;
        x2 = y1 + y3;
        x3 = y1 - y3;
        StorePoint(x0 + x2, a0, a1, 0);
        StorePoint(x0 - x2, a0, a1, 1);
        StorePoint(x1 + Turn(x3), a0, a1, 2);
        StorePoint(x1 - Turn(x3), a0, a1, 3);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void CftF162Pair(double* a0, double* a1, double* w)
    {
        Vector256<double> wn4r = Vector256.Create(w[1]);
        Vector256<double> wk1r = Vector256.Create(w[4]);
        Vector256<double> wk1i = Vector256.Create(w[5]);
        Vector256<double> wk3r = Vector256.Create(w[6]);
        Vector256<double> wk3i = Vector256.Create(-w[7]);
        Vector256<double> wk2r = Vector256.Create(w[8]);
        Vector256<double> wk2i = Vector256.Create(w[9]);
        Vector256<double> c0 = LoadPoint(a0, a1, 0);
        Vector256<double> c8 = LoadPoint(a0, a1, 8);
        Vector256<double> c4 = LoadPoint(a0, a1, 4);
        Vector256<double> c12 = LoadPoint(a0, a1, 12);
        Vector256<double> x1 = c0 + Turn(c8);
        Vector256<double> x0 = c4 + Turn(c12);
        Vector256<double> x2 = wn4r * (x0 + Turn(x0));
        Vector256<double> y0 = x1 + x2;
        Vector256<double> y4 = x1 - x2;
        x1 = c0 - Turn(c8);
        x0 = c4 - Turn(c12);
        x2 = wn4r * (x0 + Turn(x0));
        Vector256<double> y8 = x1 + Turn(x2);
        Vector256<double> y12 = x1 - Turn(x2);
        Vector256<double> c1 = LoadPoint(a0, a1, 1);
        Vector256<double> c9 = LoadPoint(a0, a1, 9);
        Vector256<double> c5 = LoadPoint(a0, a1, 5);
        Vector256<double> c13 = LoadPoint(a0, a1, 13);
        x1 = Rotate(c1 + Turn(c9), wk1r, wk1i);
        x2 = Rotate(c5 + Turn(c13), wk3i, wk3r);
        Vector256<double> y1 = x1 + x2;
        Vector256<double> y5 = x1 - x2;
        x1 = Rotate(c1 - Turn(c9), wk3r, wk3i);
        x2 = RotateConjugate(c5 - Turn(c13), wk1r, wk1i);
        Vector256<double> y9 = x1 - x2;
        Vector256<double> y13 = x1 + x2;
        Vector256<double> c2 = LoadPoint(a0, a1, 2);
        Vector256<double> c10 = LoadPoint(a0, a1, 10);
        Vector256<double> c6 = LoadPoint(a0, a1, 6);
        Vector256<double> c14 = LoadPoint(a0, a1, 14);
        x1 = Rotate(c2 + Turn(c10), wk2r, wk2i);
        x2 = Rotate(c6 + Turn(c14), wk2i, wk2r);
        Vector256<double> y2 = x1 + x2;
        Vector256<double> y6 = x1 - x2;
        x1 = Rotate(c2 - Turn(c10), wk2i, wk2r);
        x2 = Rotate(c6 - Turn(c14), wk2r, wk2i);
        Vector256<double> y10 = x1 - x2;
        Vector256<double> y14 = x1 + x2;
        Vector256<double> c3 = LoadPoint(a0, a1, 3);
        Vector256<double> c11 = LoadPoint(a0, a1, 11);
        Vector256<double> c7 = LoadPoint(a0, a1, 7);
        Vector256<double> c15 = LoadPoint(a0, a1, 15);
        x1 = Rotate(c3 + Turn(c11), wk3r, wk3i);
        x2 = Rotate(c7 + Turn(c15), wk1i, wk1r);
        Vector256<double> y3 = x1 + x2;
        Vector256<double> y7 = x1 - x2;
        x1 = RotateConjugate(c3 - Turn(c11), wk1i, wk1r);
        x2 = Rotate(c7 - Turn(c15), wk3i, wk3r);
        Vector256<double> y11 = x1 + x2;
        Vector256<double> y15 = x1 - x2;
        x1 = y0 + y2;
        x2 = y1 + y3;
        StorePoint(x1 + x2, a0, a1, 0);
        StorePoint(x1 - x2, a0, a1, 1);
        x1 = y0 - y2;
        x2 = y1 - y3;
        StorePoint(x1 + Turn(x2), a0, a1, 2);
        StorePoint(x1 - Turn(x2), a0, a1, 3);
        x1 = y4 + Turn(y6);
        x0 = y5 + Turn(y7);
        x2 = wn4r * (x0 + Turn(x0));
        StorePoint(x1 + x2, a0, a1, 4);
        StorePoint(x1 - x2, a0, a1, 5);
        x1 = y4 - Turn(y6);
        x0 = y5 - Turn(y7);
        x2 = wn4r * (x0 + Turn(x0));
        StorePoint(x1 + Turn(x2), a0, a1, 6);
        StorePoint(x1 - Turn(x2), a0, a1, 7);
        x1 = y8 + y10;
        x2 = y9 - y11;
        StorePoint(x1 + x2, a0, a1, 8);
        StorePoint(x1 - x2, a0, a1, 9);
        x1 = y8 - y10;
        x2 = y9 + y11;
        StorePoint(x1 + Turn(x2), a0, a1, 10);
        StorePoint(x1 - Turn(x2), a0, a1, 11);
        x1 = y12 + Turn(y14);
        x0 = y13 - Turn(y15);
        x2 = wn4r * (x0 + Turn(x0));
        StorePoint(x1 + x2, a0, a1, 12);
        StorePoint(x1 - x2, a0, a1, 13);
        x1 = y12 - Turn(y14);
        x0 = y13 + Turn(y15);
        x2 = wn4r * (x0 + Turn(x0));
        StorePoint(x1 + Turn(x2), a0, a1, 14);
        StorePoint(x1 - Turn(x2), a0, a1, 15);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void CftLeaf512Pairs(int isplt, double* a, int nw, double* w)
    {
        CftMdl1(128, a, &w[nw - 64]);
        CftMdl2(128, &a[128], &w[nw - 128]);
        CftMdl1(128, &a[256], &w[nw - 64]);
        if (isplt != 0)
        {
            CftMdl1(128, &a[384], &w[nw - 64]);
        }
        else
        {
            CftMdl2(128, &a[384], &w[nw - 128]);
        }
        double* w161 = &w[nw - 8];
        double* w162 = &w[nw - 32];
        CftF161Pair(a, &a[64], w161);
        CftF161Pair(&a[96], &a[128], w161);
        CftF161Pair(&a[192], &a[256], w161);
        CftF161Pair(&a[320], &a[352], w161);
        CftF161Pair(&a[384], &a[448], w161);
        CftF162Pair(&a[32], &a[160], w162);
        CftF162Pair(&a[224], &a[288], w162);
        if (isplt != 0)
        {
            CftF161Pair(&a[480], &a[480], w161);
            CftF162Pair(&a[416], &a[416], w162);
        }
        else
        {
            CftF162Pair(&a[416], &a[480], w162);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void CftLeaf256Pairs(int isplt, double* a, int nw, double* w)
    {
        CftMdl1(64, a, &w[nw - 32]);
        CftMdl2(64, &a[64], &w[nw - 64]);
        CftMdl1(64, &a[128], &w[nw - 32]);
        if (isplt != 0)
        {
            CftMdl1(64, &a[192], &w[nw - 32]);
        }
        else
        {
            CftMdl2(64, &a[192], &w[nw - 64]);
        }
        double* w081 = &w[nw - 8];
        CftF081Pair(a, &a[32], w081);
        CftF081Pair(&a[48], &a[64], w081);
        CftF081Pair(&a[96], &a[128], w081);
        CftF081Pair(&a[160], &a[176], w081);
        CftF081Pair(&a[192], &a[224], w081);
        CftF082Pair(&a[16], &a[80], w081);
        CftF082Pair(&a[112], &a[144], w081);
        if (isplt != 0)
        {
            CftF081Pair(&a[240], &a[240], w081);
            CftF082Pair(&a[208], &a[208], w081);
        }
        else
        {
            CftF082Pair(&a[208], &a[240], w081);
        }
    }
}
