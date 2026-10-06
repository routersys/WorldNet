using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace WorldNet;

internal static unsafe partial class OouraFft
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<double> LoadPoint(double* a, int index)
    {
        return Vector128.Load(a + (2 * index));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StorePoint(Vector128<double> value, double* a, int index)
    {
        value.Store(a + (2 * index));
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void CftF081Narrow(double* a, double* w)
    {
        Vector128<double> wn4r = Vector128.Create(w[1]);
        Vector128<double> c0 = LoadPoint(a, 0);
        Vector128<double> c4 = LoadPoint(a, 4);
        Vector128<double> c2 = LoadPoint(a, 2);
        Vector128<double> c6 = LoadPoint(a, 6);
        Vector128<double> x0 = c0 + c4;
        Vector128<double> x1 = c0 - c4;
        Vector128<double> x2 = c2 + c6;
        Vector128<double> x3 = c2 - c6;
        Vector128<double> y0 = x0 + x2;
        Vector128<double> y2 = x0 - x2;
        Vector128<double> y1 = x1 + Turn(x3);
        Vector128<double> y3 = x1 - Turn(x3);
        Vector128<double> c1 = LoadPoint(a, 1);
        Vector128<double> c5 = LoadPoint(a, 5);
        Vector128<double> c3 = LoadPoint(a, 3);
        Vector128<double> c7 = LoadPoint(a, 7);
        x0 = c1 + c5;
        x1 = c1 - c5;
        x2 = c3 + c7;
        x3 = c3 - c7;
        Vector128<double> y4 = x0 + x2;
        Vector128<double> y6 = x0 - x2;
        x0 = x1 + Turn(x3);
        x2 = x1 - Turn(x3);
        Vector128<double> y5 = wn4r * (x0 + Turn(x0));
        Vector128<double> y7 = wn4r * (x2 + Turn(x2));
        StorePoint(y1 + y5, a, 4);
        StorePoint(y1 - y5, a, 5);
        StorePoint(y3 + Turn(y7), a, 6);
        StorePoint(y3 - Turn(y7), a, 7);
        StorePoint(y0 + y4, a, 0);
        StorePoint(y0 - y4, a, 1);
        StorePoint(y2 + Turn(y6), a, 2);
        StorePoint(y2 - Turn(y6), a, 3);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void CftF082Narrow(double* a, double* w)
    {
        Vector128<double> wn4r = Vector128.Create(w[1]);
        Vector128<double> wk1r = Vector128.Create(w[2]);
        Vector128<double> wk1i = Vector128.Create(w[3]);
        Vector128<double> c0 = LoadPoint(a, 0);
        Vector128<double> c4 = LoadPoint(a, 4);
        Vector128<double> y0 = c0 + Turn(c4);
        Vector128<double> y1 = c0 - Turn(c4);
        Vector128<double> c2 = LoadPoint(a, 2);
        Vector128<double> c6 = LoadPoint(a, 6);
        Vector128<double> x0 = c2 + Turn(c6);
        Vector128<double> y2 = wn4r * (x0 + Turn(x0));
        x0 = c2 - Turn(c6);
        Vector128<double> y3 = wn4r * (x0 + Turn(x0));
        Vector128<double> c1 = LoadPoint(a, 1);
        Vector128<double> c5 = LoadPoint(a, 5);
        Vector128<double> y4 = Rotate(c1 + Turn(c5), wk1r, wk1i);
        Vector128<double> y5 = Rotate(c1 - Turn(c5), wk1i, wk1r);
        Vector128<double> c3 = LoadPoint(a, 3);
        Vector128<double> c7 = LoadPoint(a, 7);
        Vector128<double> y6 = Rotate(c3 + Turn(c7), wk1i, wk1r);
        Vector128<double> y7 = Rotate(c3 - Turn(c7), wk1r, wk1i);
        x0 = y0 + y2;
        Vector128<double> x1 = y4 + y6;
        StorePoint(x0 + x1, a, 0);
        StorePoint(x0 - x1, a, 1);
        x0 = y0 - y2;
        x1 = y4 - y6;
        StorePoint(x0 + Turn(x1), a, 2);
        StorePoint(x0 - Turn(x1), a, 3);
        x0 = y1 + Turn(y3);
        x1 = y5 - y7;
        StorePoint(x0 + x1, a, 4);
        StorePoint(x0 - x1, a, 5);
        x0 = y1 - Turn(y3);
        x1 = y5 + y7;
        StorePoint(x0 + Turn(x1), a, 6);
        StorePoint(x0 - Turn(x1), a, 7);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void CftF161Narrow(double* a, double* w)
    {
        Vector128<double> wn4r = Vector128.Create(w[1]);
        Vector128<double> wk1r = Vector128.Create(w[2]);
        Vector128<double> wk1i = Vector128.Create(w[3]);
        Vector128<double> x0 = LoadPoint(a, 0) + LoadPoint(a, 8);
        Vector128<double> x1 = LoadPoint(a, 0) - LoadPoint(a, 8);
        Vector128<double> x2 = LoadPoint(a, 4) + LoadPoint(a, 12);
        Vector128<double> x3 = LoadPoint(a, 4) - LoadPoint(a, 12);
        Vector128<double> y0 = x0 + x2;
        Vector128<double> y4 = x0 - x2;
        Vector128<double> y8 = x1 + Turn(x3);
        Vector128<double> y12 = x1 - Turn(x3);
        x0 = LoadPoint(a, 1) + LoadPoint(a, 9);
        x1 = LoadPoint(a, 1) - LoadPoint(a, 9);
        x2 = LoadPoint(a, 5) + LoadPoint(a, 13);
        x3 = LoadPoint(a, 5) - LoadPoint(a, 13);
        Vector128<double> y1 = x0 + x2;
        Vector128<double> y5 = x0 - x2;
        Vector128<double> y9 = Rotate(x1 + Turn(x3), wk1r, wk1i);
        Vector128<double> y13 = Rotate(x1 - Turn(x3), wk1i, wk1r);
        x0 = LoadPoint(a, 2) + LoadPoint(a, 10);
        x1 = LoadPoint(a, 2) - LoadPoint(a, 10);
        x2 = LoadPoint(a, 6) + LoadPoint(a, 14);
        x3 = LoadPoint(a, 6) - LoadPoint(a, 14);
        Vector128<double> y2 = x0 + x2;
        Vector128<double> y6 = x0 - x2;
        x0 = x1 + Turn(x3);
        Vector128<double> y10 = wn4r * (x0 + Turn(x0));
        x0 = x1 - Turn(x3);
        Vector128<double> y14 = wn4r * (x0 - Turn(x0));
        x0 = LoadPoint(a, 3) + LoadPoint(a, 11);
        x1 = LoadPoint(a, 3) - LoadPoint(a, 11);
        x2 = LoadPoint(a, 7) + LoadPoint(a, 15);
        x3 = LoadPoint(a, 7) - LoadPoint(a, 15);
        Vector128<double> y3 = x0 + x2;
        Vector128<double> y7 = x0 - x2;
        Vector128<double> y11 = Rotate(x1 + Turn(x3), wk1i, wk1r);
        Vector128<double> y15 = Rotate(x1 - Turn(x3), wk1r, wk1i);
        x0 = y12 - y14;
        x1 = y12 + y14;
        x2 = y13 - y15;
        x3 = y13 + y15;
        StorePoint(x0 + x2, a, 12);
        StorePoint(x0 - x2, a, 13);
        StorePoint(x1 + Turn(x3), a, 14);
        StorePoint(x1 - Turn(x3), a, 15);
        x0 = y8 + y10;
        x1 = y8 - y10;
        x2 = y9 + y11;
        x3 = y9 - y11;
        StorePoint(x0 + x2, a, 8);
        StorePoint(x0 - x2, a, 9);
        StorePoint(x1 + Turn(x3), a, 10);
        StorePoint(x1 - Turn(x3), a, 11);
        x0 = y5 + Turn(y7);
        x2 = wn4r * (x0 + Turn(x0));
        x0 = y5 - Turn(y7);
        x3 = wn4r * (x0 + Turn(x0));
        x0 = y4 + Turn(y6);
        x1 = y4 - Turn(y6);
        StorePoint(x0 + x2, a, 4);
        StorePoint(x0 - x2, a, 5);
        StorePoint(x1 + Turn(x3), a, 6);
        StorePoint(x1 - Turn(x3), a, 7);
        x0 = y0 + y2;
        x1 = y0 - y2;
        x2 = y1 + y3;
        x3 = y1 - y3;
        StorePoint(x0 + x2, a, 0);
        StorePoint(x0 - x2, a, 1);
        StorePoint(x1 + Turn(x3), a, 2);
        StorePoint(x1 - Turn(x3), a, 3);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void CftF162Narrow(double* a, double* w)
    {
        Vector128<double> wn4r = Vector128.Create(w[1]);
        Vector128<double> wk1r = Vector128.Create(w[4]);
        Vector128<double> wk1i = Vector128.Create(w[5]);
        Vector128<double> wk3r = Vector128.Create(w[6]);
        Vector128<double> wk3i = Vector128.Create(-w[7]);
        Vector128<double> wk2r = Vector128.Create(w[8]);
        Vector128<double> wk2i = Vector128.Create(w[9]);
        Vector128<double> c0 = LoadPoint(a, 0);
        Vector128<double> c8 = LoadPoint(a, 8);
        Vector128<double> c4 = LoadPoint(a, 4);
        Vector128<double> c12 = LoadPoint(a, 12);
        Vector128<double> x1 = c0 + Turn(c8);
        Vector128<double> x0 = c4 + Turn(c12);
        Vector128<double> x2 = wn4r * (x0 + Turn(x0));
        Vector128<double> y0 = x1 + x2;
        Vector128<double> y4 = x1 - x2;
        x1 = c0 - Turn(c8);
        x0 = c4 - Turn(c12);
        x2 = wn4r * (x0 + Turn(x0));
        Vector128<double> y8 = x1 + Turn(x2);
        Vector128<double> y12 = x1 - Turn(x2);
        Vector128<double> c1 = LoadPoint(a, 1);
        Vector128<double> c9 = LoadPoint(a, 9);
        Vector128<double> c5 = LoadPoint(a, 5);
        Vector128<double> c13 = LoadPoint(a, 13);
        x1 = Rotate(c1 + Turn(c9), wk1r, wk1i);
        x2 = Rotate(c5 + Turn(c13), wk3i, wk3r);
        Vector128<double> y1 = x1 + x2;
        Vector128<double> y5 = x1 - x2;
        x1 = Rotate(c1 - Turn(c9), wk3r, wk3i);
        x2 = RotateConjugate(c5 - Turn(c13), wk1r, wk1i);
        Vector128<double> y9 = x1 - x2;
        Vector128<double> y13 = x1 + x2;
        Vector128<double> c2 = LoadPoint(a, 2);
        Vector128<double> c10 = LoadPoint(a, 10);
        Vector128<double> c6 = LoadPoint(a, 6);
        Vector128<double> c14 = LoadPoint(a, 14);
        x1 = Rotate(c2 + Turn(c10), wk2r, wk2i);
        x2 = Rotate(c6 + Turn(c14), wk2i, wk2r);
        Vector128<double> y2 = x1 + x2;
        Vector128<double> y6 = x1 - x2;
        x1 = Rotate(c2 - Turn(c10), wk2i, wk2r);
        x2 = Rotate(c6 - Turn(c14), wk2r, wk2i);
        Vector128<double> y10 = x1 - x2;
        Vector128<double> y14 = x1 + x2;
        Vector128<double> c3 = LoadPoint(a, 3);
        Vector128<double> c11 = LoadPoint(a, 11);
        Vector128<double> c7 = LoadPoint(a, 7);
        Vector128<double> c15 = LoadPoint(a, 15);
        x1 = Rotate(c3 + Turn(c11), wk3r, wk3i);
        x2 = Rotate(c7 + Turn(c15), wk1i, wk1r);
        Vector128<double> y3 = x1 + x2;
        Vector128<double> y7 = x1 - x2;
        x1 = RotateConjugate(c3 - Turn(c11), wk1i, wk1r);
        x2 = Rotate(c7 - Turn(c15), wk3i, wk3r);
        Vector128<double> y11 = x1 + x2;
        Vector128<double> y15 = x1 - x2;
        x1 = y0 + y2;
        x2 = y1 + y3;
        StorePoint(x1 + x2, a, 0);
        StorePoint(x1 - x2, a, 1);
        x1 = y0 - y2;
        x2 = y1 - y3;
        StorePoint(x1 + Turn(x2), a, 2);
        StorePoint(x1 - Turn(x2), a, 3);
        x1 = y4 + Turn(y6);
        x0 = y5 + Turn(y7);
        x2 = wn4r * (x0 + Turn(x0));
        StorePoint(x1 + x2, a, 4);
        StorePoint(x1 - x2, a, 5);
        x1 = y4 - Turn(y6);
        x0 = y5 - Turn(y7);
        x2 = wn4r * (x0 + Turn(x0));
        StorePoint(x1 + Turn(x2), a, 6);
        StorePoint(x1 - Turn(x2), a, 7);
        x1 = y8 + y10;
        x2 = y9 - y11;
        StorePoint(x1 + x2, a, 8);
        StorePoint(x1 - x2, a, 9);
        x1 = y8 - y10;
        x2 = y9 + y11;
        StorePoint(x1 + Turn(x2), a, 10);
        StorePoint(x1 - Turn(x2), a, 11);
        x1 = y12 + Turn(y14);
        x0 = y13 - Turn(y15);
        x2 = wn4r * (x0 + Turn(x0));
        StorePoint(x1 + x2, a, 12);
        StorePoint(x1 - x2, a, 13);
        x1 = y12 - Turn(y14);
        x0 = y13 + Turn(y15);
        x2 = wn4r * (x0 + Turn(x0));
        StorePoint(x1 + Turn(x2), a, 14);
        StorePoint(x1 - Turn(x2), a, 15);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void CftLeafNarrow(int n, int isplt, double* a, int nw, double* w)
    {
        if (n == 512)
        {
            CftMdl1(128, a, &w[nw - 64]);
            CftF161Narrow(a, &w[nw - 8]);
            CftF162Narrow(&a[32], &w[nw - 32]);
            CftF161Narrow(&a[64], &w[nw - 8]);
            CftF161Narrow(&a[96], &w[nw - 8]);
            CftMdl2(128, &a[128], &w[nw - 128]);
            CftF161Narrow(&a[128], &w[nw - 8]);
            CftF162Narrow(&a[160], &w[nw - 32]);
            CftF161Narrow(&a[192], &w[nw - 8]);
            CftF162Narrow(&a[224], &w[nw - 32]);
            CftMdl1(128, &a[256], &w[nw - 64]);
            CftF161Narrow(&a[256], &w[nw - 8]);
            CftF162Narrow(&a[288], &w[nw - 32]);
            CftF161Narrow(&a[320], &w[nw - 8]);
            CftF161Narrow(&a[352], &w[nw - 8]);
            if (isplt != 0)
            {
                CftMdl1(128, &a[384], &w[nw - 64]);
                CftF161Narrow(&a[480], &w[nw - 8]);
            }
            else
            {
                CftMdl2(128, &a[384], &w[nw - 128]);
                CftF162Narrow(&a[480], &w[nw - 32]);
            }
            CftF161Narrow(&a[384], &w[nw - 8]);
            CftF162Narrow(&a[416], &w[nw - 32]);
            CftF161Narrow(&a[448], &w[nw - 8]);
        }
        else
        {
            CftMdl1(64, a, &w[nw - 32]);
            CftF081Narrow(a, &w[nw - 8]);
            CftF082Narrow(&a[16], &w[nw - 8]);
            CftF081Narrow(&a[32], &w[nw - 8]);
            CftF081Narrow(&a[48], &w[nw - 8]);
            CftMdl2(64, &a[64], &w[nw - 64]);
            CftF081Narrow(&a[64], &w[nw - 8]);
            CftF082Narrow(&a[80], &w[nw - 8]);
            CftF081Narrow(&a[96], &w[nw - 8]);
            CftF082Narrow(&a[112], &w[nw - 8]);
            CftMdl1(64, &a[128], &w[nw - 32]);
            CftF081Narrow(&a[128], &w[nw - 8]);
            CftF082Narrow(&a[144], &w[nw - 8]);
            CftF081Narrow(&a[160], &w[nw - 8]);
            CftF081Narrow(&a[176], &w[nw - 8]);
            if (isplt != 0)
            {
                CftMdl1(64, &a[192], &w[nw - 32]);
                CftF081Narrow(&a[240], &w[nw - 8]);
            }
            else
            {
                CftMdl2(64, &a[192], &w[nw - 64]);
                CftF082Narrow(&a[240], &w[nw - 8]);
            }
            CftF081Narrow(&a[192], &w[nw - 8]);
            CftF082Narrow(&a[208], &w[nw - 8]);
            CftF081Narrow(&a[224], &w[nw - 8]);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void CftFx41Narrow(int n, double* a, int nw, double* w)
    {
        if (n == 128)
        {
            CftF161Narrow(a, &w[nw - 8]);
            CftF162Narrow(&a[32], &w[nw - 32]);
            CftF161Narrow(&a[64], &w[nw - 8]);
            CftF161Narrow(&a[96], &w[nw - 8]);
        }
        else
        {
            CftF081Narrow(a, &w[nw - 8]);
            CftF082Narrow(&a[16], &w[nw - 8]);
            CftF081Narrow(&a[32], &w[nw - 8]);
            CftF081Narrow(&a[48], &w[nw - 8]);
        }
    }
}
