namespace WorldNet;

internal static unsafe partial class OouraFft
{
    private static void CftRec4(int n, double* a, int nw, double* w)
    {
        int m = n;
        while (m > 512)
        {
            m >>= 2;
            CftMdl1(m, &a[n - m], &w[nw - (m >> 1)]);
        }
        CftLeaf(m, 1, &a[n - m], nw, w);
        int k = 0;
        for (int j = n - m; j > 0; j -= m)
        {
            k++;
            int isplt = CftTree(m, j, k, a, nw, w);
            CftLeaf(m, isplt, &a[j - m], nw, w);
        }
    }

    private static int CftTree(int n, int j, int k, double* a, int nw, double* w)
    {
        int isplt;
        if ((k & 3) != 0)
        {
            isplt = k & 1;
            if (isplt != 0)
            {
                CftMdl1(n, &a[j - n], &w[nw - (n >> 1)]);
            }
            else
            {
                CftMdl2(n, &a[j - n], &w[nw - n]);
            }
        }
        else
        {
            int m = n;
            int i;
            for (i = k; (i & 3) == 0; i >>= 2)
            {
                m <<= 2;
            }
            isplt = i & 1;
            if (isplt != 0)
            {
                while (m > 128)
                {
                    CftMdl1(m, &a[j - m], &w[nw - (m >> 1)]);
                    m >>= 2;
                }
            }
            else
            {
                while (m > 128)
                {
                    CftMdl2(m, &a[j - m], &w[nw - m]);
                    m >>= 2;
                }
            }
        }
        return isplt;
    }

    private static void CftLeaf(int n, int isplt, double* a, int nw, double* w)
    {
        if (n == 512)
        {
            CftMdl1(128, a, &w[nw - 64]);
            CftF161(a, &w[nw - 8]);
            CftF162(&a[32], &w[nw - 32]);
            CftF161(&a[64], &w[nw - 8]);
            CftF161(&a[96], &w[nw - 8]);
            CftMdl2(128, &a[128], &w[nw - 128]);
            CftF161(&a[128], &w[nw - 8]);
            CftF162(&a[160], &w[nw - 32]);
            CftF161(&a[192], &w[nw - 8]);
            CftF162(&a[224], &w[nw - 32]);
            CftMdl1(128, &a[256], &w[nw - 64]);
            CftF161(&a[256], &w[nw - 8]);
            CftF162(&a[288], &w[nw - 32]);
            CftF161(&a[320], &w[nw - 8]);
            CftF161(&a[352], &w[nw - 8]);
            if (isplt != 0)
            {
                CftMdl1(128, &a[384], &w[nw - 64]);
                CftF161(&a[480], &w[nw - 8]);
            }
            else
            {
                CftMdl2(128, &a[384], &w[nw - 128]);
                CftF162(&a[480], &w[nw - 32]);
            }
            CftF161(&a[384], &w[nw - 8]);
            CftF162(&a[416], &w[nw - 32]);
            CftF161(&a[448], &w[nw - 8]);
        }
        else
        {
            CftMdl1(64, a, &w[nw - 32]);
            CftF081(a, &w[nw - 8]);
            CftF082(&a[16], &w[nw - 8]);
            CftF081(&a[32], &w[nw - 8]);
            CftF081(&a[48], &w[nw - 8]);
            CftMdl2(64, &a[64], &w[nw - 64]);
            CftF081(&a[64], &w[nw - 8]);
            CftF082(&a[80], &w[nw - 8]);
            CftF081(&a[96], &w[nw - 8]);
            CftF082(&a[112], &w[nw - 8]);
            CftMdl1(64, &a[128], &w[nw - 32]);
            CftF081(&a[128], &w[nw - 8]);
            CftF082(&a[144], &w[nw - 8]);
            CftF081(&a[160], &w[nw - 8]);
            CftF081(&a[176], &w[nw - 8]);
            if (isplt != 0)
            {
                CftMdl1(64, &a[192], &w[nw - 32]);
                CftF081(&a[240], &w[nw - 8]);
            }
            else
            {
                CftMdl2(64, &a[192], &w[nw - 64]);
                CftF082(&a[240], &w[nw - 8]);
            }
            CftF081(&a[192], &w[nw - 8]);
            CftF082(&a[208], &w[nw - 8]);
            CftF081(&a[224], &w[nw - 8]);
        }
    }

    private static void CftFx41(int n, double* a, int nw, double* w)
    {
        if (n == 128)
        {
            CftF161(a, &w[nw - 8]);
            CftF162(&a[32], &w[nw - 32]);
            CftF161(&a[64], &w[nw - 8]);
            CftF161(&a[96], &w[nw - 8]);
        }
        else
        {
            CftF081(a, &w[nw - 8]);
            CftF082(&a[16], &w[nw - 8]);
            CftF081(&a[32], &w[nw - 8]);
            CftF081(&a[48], &w[nw - 8]);
        }
    }
}
