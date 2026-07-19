using System.Runtime.CompilerServices;

namespace WorldNet;

internal static unsafe partial class OouraFft
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SwapComplex(double* a, int j1, int k1)
    {
        double xr = a[j1];
        double xi = a[j1 + 1];
        double yr = a[k1];
        double yi = a[k1 + 1];
        a[j1] = yr;
        a[j1 + 1] = yi;
        a[k1] = xr;
        a[k1 + 1] = xi;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SwapComplexConjugate(double* a, int j1, int k1)
    {
        double xr = a[j1];
        double xi = -a[j1 + 1];
        double yr = a[k1];
        double yi = -a[k1 + 1];
        a[j1] = yr;
        a[j1 + 1] = yi;
        a[k1] = xr;
        a[k1 + 1] = xi;
    }

    private static void BitRv2(int n, int* ip, double* a)
    {
        int m = 1;
        int l;
        for (l = n >> 2; l > 8; l >>= 2)
        {
            m <<= 1;
        }
        int nh = n >> 1;
        int nm = 4 * m;

        if (l == 8)
        {
            for (int k = 0; k < m; k++)
            {
                for (int j = 0; j < k; j++)
                {
                    int j1 = 4 * j + 2 * ip[m + k];
                    int k1 = 4 * k + 2 * ip[m + j];
                    SwapComplex(a, j1, k1);
                    j1 += nm;
                    k1 += 2 * nm;
                    SwapComplex(a, j1, k1);
                    j1 += nm;
                    k1 -= nm;
                    SwapComplex(a, j1, k1);
                    j1 += nm;
                    k1 += 2 * nm;
                    SwapComplex(a, j1, k1);
                    j1 += nh;
                    k1 += 2;
                    SwapComplex(a, j1, k1);
                    j1 -= nm;
                    k1 -= 2 * nm;
                    SwapComplex(a, j1, k1);
                    j1 -= nm;
                    k1 += nm;
                    SwapComplex(a, j1, k1);
                    j1 -= nm;
                    k1 -= 2 * nm;
                    SwapComplex(a, j1, k1);
                    j1 += 2;
                    k1 += nh;
                    SwapComplex(a, j1, k1);
                    j1 += nm;
                    k1 += 2 * nm;
                    SwapComplex(a, j1, k1);
                    j1 += nm;
                    k1 -= nm;
                    SwapComplex(a, j1, k1);
                    j1 += nm;
                    k1 += 2 * nm;
                    SwapComplex(a, j1, k1);
                    j1 -= nh;
                    k1 -= 2;
                    SwapComplex(a, j1, k1);
                    j1 -= nm;
                    k1 -= 2 * nm;
                    SwapComplex(a, j1, k1);
                    j1 -= nm;
                    k1 += nm;
                    SwapComplex(a, j1, k1);
                    j1 -= nm;
                    k1 -= 2 * nm;
                    SwapComplex(a, j1, k1);
                }

                int tailK1 = 4 * k + 2 * ip[m + k];
                int tailJ1 = tailK1 + 2;
                tailK1 += nh;
                SwapComplex(a, tailJ1, tailK1);
                tailJ1 += nm;
                tailK1 += 2 * nm;
                SwapComplex(a, tailJ1, tailK1);
                tailJ1 += nm;
                tailK1 -= nm;
                SwapComplex(a, tailJ1, tailK1);
                tailJ1 -= 2;
                tailK1 -= nh;
                SwapComplex(a, tailJ1, tailK1);
                tailJ1 += nh + 2;
                tailK1 += nh + 2;
                SwapComplex(a, tailJ1, tailK1);
                tailJ1 -= nh - nm;
                tailK1 += 2 * nm - 2;
                SwapComplex(a, tailJ1, tailK1);
            }
        }
        else
        {
            for (int k = 0; k < m; k++)
            {
                for (int j = 0; j < k; j++)
                {
                    int j1 = 4 * j + ip[m + k];
                    int k1 = 4 * k + ip[m + j];
                    SwapComplex(a, j1, k1);
                    j1 += nm;
                    k1 += nm;
                    SwapComplex(a, j1, k1);
                    j1 += nh;
                    k1 += 2;
                    SwapComplex(a, j1, k1);
                    j1 -= nm;
                    k1 -= nm;
                    SwapComplex(a, j1, k1);
                    j1 += 2;
                    k1 += nh;
                    SwapComplex(a, j1, k1);
                    j1 += nm;
                    k1 += nm;
                    SwapComplex(a, j1, k1);
                    j1 -= nh;
                    k1 -= 2;
                    SwapComplex(a, j1, k1);
                    j1 -= nm;
                    k1 -= nm;
                    SwapComplex(a, j1, k1);
                }

                int tailK1 = 4 * k + ip[m + k];
                int tailJ1 = tailK1 + 2;
                tailK1 += nh;
                SwapComplex(a, tailJ1, tailK1);
                tailJ1 += nm;
                tailK1 += nm;
                SwapComplex(a, tailJ1, tailK1);
            }
        }
    }

    private static void BitRv2Conj(int n, int* ip, double* a)
    {
        int m = 1;
        int l;
        for (l = n >> 2; l > 8; l >>= 2)
        {
            m <<= 1;
        }
        int nh = n >> 1;
        int nm = 4 * m;

        if (l == 8)
        {
            for (int k = 0; k < m; k++)
            {
                for (int j = 0; j < k; j++)
                {
                    int j1 = 4 * j + 2 * ip[m + k];
                    int k1 = 4 * k + 2 * ip[m + j];
                    SwapComplexConjugate(a, j1, k1);
                    j1 += nm;
                    k1 += 2 * nm;
                    SwapComplexConjugate(a, j1, k1);
                    j1 += nm;
                    k1 -= nm;
                    SwapComplexConjugate(a, j1, k1);
                    j1 += nm;
                    k1 += 2 * nm;
                    SwapComplexConjugate(a, j1, k1);
                    j1 += nh;
                    k1 += 2;
                    SwapComplexConjugate(a, j1, k1);
                    j1 -= nm;
                    k1 -= 2 * nm;
                    SwapComplexConjugate(a, j1, k1);
                    j1 -= nm;
                    k1 += nm;
                    SwapComplexConjugate(a, j1, k1);
                    j1 -= nm;
                    k1 -= 2 * nm;
                    SwapComplexConjugate(a, j1, k1);
                    j1 += 2;
                    k1 += nh;
                    SwapComplexConjugate(a, j1, k1);
                    j1 += nm;
                    k1 += 2 * nm;
                    SwapComplexConjugate(a, j1, k1);
                    j1 += nm;
                    k1 -= nm;
                    SwapComplexConjugate(a, j1, k1);
                    j1 += nm;
                    k1 += 2 * nm;
                    SwapComplexConjugate(a, j1, k1);
                    j1 -= nh;
                    k1 -= 2;
                    SwapComplexConjugate(a, j1, k1);
                    j1 -= nm;
                    k1 -= 2 * nm;
                    SwapComplexConjugate(a, j1, k1);
                    j1 -= nm;
                    k1 += nm;
                    SwapComplexConjugate(a, j1, k1);
                    j1 -= nm;
                    k1 -= 2 * nm;
                    SwapComplexConjugate(a, j1, k1);
                }

                int tailK1 = 4 * k + 2 * ip[m + k];
                int tailJ1 = tailK1 + 2;
                tailK1 += nh;
                a[tailJ1 - 1] = -a[tailJ1 - 1];
                SwapComplexConjugate(a, tailJ1, tailK1);
                a[tailK1 + 3] = -a[tailK1 + 3];
                tailJ1 += nm;
                tailK1 += 2 * nm;
                SwapComplexConjugate(a, tailJ1, tailK1);
                tailJ1 += nm;
                tailK1 -= nm;
                SwapComplexConjugate(a, tailJ1, tailK1);
                tailJ1 -= 2;
                tailK1 -= nh;
                SwapComplexConjugate(a, tailJ1, tailK1);
                tailJ1 += nh + 2;
                tailK1 += nh + 2;
                SwapComplexConjugate(a, tailJ1, tailK1);
                tailJ1 -= nh - nm;
                tailK1 += 2 * nm - 2;
                a[tailJ1 - 1] = -a[tailJ1 - 1];
                SwapComplexConjugate(a, tailJ1, tailK1);
                a[tailK1 + 3] = -a[tailK1 + 3];
            }
        }
        else
        {
            for (int k = 0; k < m; k++)
            {
                for (int j = 0; j < k; j++)
                {
                    int j1 = 4 * j + ip[m + k];
                    int k1 = 4 * k + ip[m + j];
                    SwapComplexConjugate(a, j1, k1);
                    j1 += nm;
                    k1 += nm;
                    SwapComplexConjugate(a, j1, k1);
                    j1 += nh;
                    k1 += 2;
                    SwapComplexConjugate(a, j1, k1);
                    j1 -= nm;
                    k1 -= nm;
                    SwapComplexConjugate(a, j1, k1);
                    j1 += 2;
                    k1 += nh;
                    SwapComplexConjugate(a, j1, k1);
                    j1 += nm;
                    k1 += nm;
                    SwapComplexConjugate(a, j1, k1);
                    j1 -= nh;
                    k1 -= 2;
                    SwapComplexConjugate(a, j1, k1);
                    j1 -= nm;
                    k1 -= nm;
                    SwapComplexConjugate(a, j1, k1);
                }

                int tailK1 = 4 * k + ip[m + k];
                int tailJ1 = tailK1 + 2;
                tailK1 += nh;
                a[tailJ1 - 1] = -a[tailJ1 - 1];
                SwapComplexConjugate(a, tailJ1, tailK1);
                a[tailK1 + 3] = -a[tailK1 + 3];
                tailJ1 += nm;
                tailK1 += nm;
                a[tailJ1 - 1] = -a[tailJ1 - 1];
                SwapComplexConjugate(a, tailJ1, tailK1);
                a[tailK1 + 3] = -a[tailK1 + 3];
            }
        }
    }

    private static void BitRv216(double* a)
    {
        double x1r = a[2];
        double x1i = a[3];
        double x2r = a[4];
        double x2i = a[5];
        double x3r = a[6];
        double x3i = a[7];
        double x4r = a[8];
        double x4i = a[9];
        double x5r = a[10];
        double x5i = a[11];
        double x7r = a[14];
        double x7i = a[15];
        double x8r = a[16];
        double x8i = a[17];
        double x10r = a[20];
        double x10i = a[21];
        double x11r = a[22];
        double x11i = a[23];
        double x12r = a[24];
        double x12i = a[25];
        double x13r = a[26];
        double x13i = a[27];
        double x14r = a[28];
        double x14i = a[29];
        a[2] = x8r;
        a[3] = x8i;
        a[4] = x4r;
        a[5] = x4i;
        a[6] = x12r;
        a[7] = x12i;
        a[8] = x2r;
        a[9] = x2i;
        a[10] = x10r;
        a[11] = x10i;
        a[14] = x14r;
        a[15] = x14i;
        a[16] = x1r;
        a[17] = x1i;
        a[20] = x5r;
        a[21] = x5i;
        a[22] = x13r;
        a[23] = x13i;
        a[24] = x3r;
        a[25] = x3i;
        a[26] = x11r;
        a[27] = x11i;
        a[28] = x7r;
        a[29] = x7i;
    }

    private static void BitRv216Neg(double* a)
    {
        double x1r = a[2];
        double x1i = a[3];
        double x2r = a[4];
        double x2i = a[5];
        double x3r = a[6];
        double x3i = a[7];
        double x4r = a[8];
        double x4i = a[9];
        double x5r = a[10];
        double x5i = a[11];
        double x6r = a[12];
        double x6i = a[13];
        double x7r = a[14];
        double x7i = a[15];
        double x8r = a[16];
        double x8i = a[17];
        double x9r = a[18];
        double x9i = a[19];
        double x10r = a[20];
        double x10i = a[21];
        double x11r = a[22];
        double x11i = a[23];
        double x12r = a[24];
        double x12i = a[25];
        double x13r = a[26];
        double x13i = a[27];
        double x14r = a[28];
        double x14i = a[29];
        double x15r = a[30];
        double x15i = a[31];
        a[2] = x15r;
        a[3] = x15i;
        a[4] = x7r;
        a[5] = x7i;
        a[6] = x11r;
        a[7] = x11i;
        a[8] = x3r;
        a[9] = x3i;
        a[10] = x13r;
        a[11] = x13i;
        a[12] = x5r;
        a[13] = x5i;
        a[14] = x9r;
        a[15] = x9i;
        a[16] = x1r;
        a[17] = x1i;
        a[18] = x14r;
        a[19] = x14i;
        a[20] = x6r;
        a[21] = x6i;
        a[22] = x10r;
        a[23] = x10i;
        a[24] = x2r;
        a[25] = x2i;
        a[26] = x12r;
        a[27] = x12i;
        a[28] = x4r;
        a[29] = x4i;
        a[30] = x8r;
        a[31] = x8i;
    }

    private static void BitRv208(double* a)
    {
        double x1r = a[2];
        double x1i = a[3];
        double x3r = a[6];
        double x3i = a[7];
        double x4r = a[8];
        double x4i = a[9];
        double x6r = a[12];
        double x6i = a[13];
        a[2] = x4r;
        a[3] = x4i;
        a[6] = x6r;
        a[7] = x6i;
        a[8] = x1r;
        a[9] = x1i;
        a[12] = x3r;
        a[13] = x3i;
    }

    private static void BitRv208Neg(double* a)
    {
        double x1r = a[2];
        double x1i = a[3];
        double x2r = a[4];
        double x2i = a[5];
        double x3r = a[6];
        double x3i = a[7];
        double x4r = a[8];
        double x4i = a[9];
        double x5r = a[10];
        double x5i = a[11];
        double x6r = a[12];
        double x6i = a[13];
        double x7r = a[14];
        double x7i = a[15];
        a[2] = x7r;
        a[3] = x7i;
        a[4] = x3r;
        a[5] = x3i;
        a[6] = x5r;
        a[7] = x5i;
        a[8] = x1r;
        a[9] = x1i;
        a[10] = x6r;
        a[11] = x6i;
        a[12] = x2r;
        a[13] = x2i;
        a[14] = x4r;
        a[15] = x4i;
    }
}
