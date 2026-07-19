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
}
