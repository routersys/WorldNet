namespace WorldNet;

internal static unsafe partial class OouraFft
{
    public static void Cdft(int n, int isgn, double* a, int* ip, double* w)
    {
        int nw = ip[0];
        if (isgn >= 0)
        {
            CftFSub(n, a, ip, nw, w);
        }
        else
        {
            CftBSub(n, a, ip, nw, w);
        }
    }

    public static void Rdft(int n, int isgn, double* a, int* ip, double* w)
    {
        int nw = ip[0];
        int nc = ip[1];

        if (isgn >= 0)
        {
            if (n > 4)
            {
                CftFSub(n, a, ip, nw, w);
                RftFSub(n, a, nc, w + nw);
            }
            else if (n == 4)
            {
                CftFSub(n, a, ip, nw, w);
            }
            double xi = a[0] - a[1];
            a[0] += a[1];
            a[1] = xi;
        }
        else
        {
            a[1] = 0.5 * (a[0] - a[1]);
            a[0] -= a[1];
            if (n > 4)
            {
                RftBSub(n, a, nc, w + nw);
                CftBSub(n, a, ip, nw, w);
            }
            else if (n == 4)
            {
                CftBSub(n, a, ip, nw, w);
            }
        }
    }

    public static void MakeWt(int nw, int* ip, double* w)
    {
        ip[0] = nw;
        ip[1] = 1;
        if (nw > 2)
        {
            int nwh = nw >> 1;
            double delta = Math.Atan(1.0) / nwh;
            double wn4r = Math.Cos(delta * nwh);
            w[0] = 1;
            w[1] = wn4r;
            if (nwh == 4)
            {
                w[2] = Math.Cos(delta * 2);
                w[3] = Math.Sin(delta * 2);
            }
            else if (nwh > 4)
            {
                MakeIpt(nw, ip);
                w[2] = 0.5 / Math.Cos(delta * 2);
                w[3] = 0.5 / Math.Cos(delta * 6);
                for (int j = 4; j < nwh; j += 4)
                {
                    w[j] = Math.Cos(delta * j);
                    w[j + 1] = Math.Sin(delta * j);
                    w[j + 2] = Math.Cos(3 * delta * j);
                    w[j + 3] = -Math.Sin(3 * delta * j);
                }
            }
            int nw0 = 0;
            while (nwh > 2)
            {
                int nw1 = nw0 + nwh;
                nwh >>= 1;
                w[nw1] = 1;
                w[nw1 + 1] = wn4r;
                if (nwh == 4)
                {
                    double wk1r = w[nw0 + 4];
                    double wk1i = w[nw0 + 5];
                    w[nw1 + 2] = wk1r;
                    w[nw1 + 3] = wk1i;
                }
                else if (nwh > 4)
                {
                    double wk1r = w[nw0 + 4];
                    double wk3r = w[nw0 + 6];
                    w[nw1 + 2] = 0.5 / wk1r;
                    w[nw1 + 3] = 0.5 / wk3r;
                    for (int j = 4; j < nwh; j += 4)
                    {
                        wk1r = w[nw0 + 2 * j];
                        double wk1i = w[nw0 + 2 * j + 1];
                        wk3r = w[nw0 + 2 * j + 2];
                        double wk3i = w[nw0 + 2 * j + 3];
                        w[nw1 + j] = wk1r;
                        w[nw1 + j + 1] = wk1i;
                        w[nw1 + j + 2] = wk3r;
                        w[nw1 + j + 3] = wk3i;
                    }
                }
                nw0 = nw1;
            }
        }
    }

    public static void MakeIpt(int nw, int* ip)
    {
        ip[2] = 0;
        ip[3] = 16;
        int m = 2;
        for (int l = nw; l > 32; l >>= 2)
        {
            int m2 = m << 1;
            int q = m2 << 3;
            for (int j = m; j < m2; j++)
            {
                int p = ip[j] << 2;
                ip[m + j] = p;
                ip[m2 + j] = p + q;
            }
            m = m2;
        }
    }

    public static void MakeCt(int nc, int* ip, double* c)
    {
        ip[1] = nc;
        if (nc > 1)
        {
            int nch = nc >> 1;
            double delta = Math.Atan(1.0) / nch;
            c[0] = Math.Cos(delta * nch);
            c[nch] = 0.5 * c[0];
            for (int j = 1; j < nch; j++)
            {
                c[j] = 0.5 * Math.Cos(delta * j);
                c[nc - j] = 0.5 * Math.Sin(delta * j);
            }
        }
    }

    private static void CftFSub(int n, double* a, int* ip, int nw, double* w)
    {
        if (n > 8)
        {
            if (n > 32)
            {
                CftF1st(n, a, &w[nw - (n >> 2)]);
                if (n > 512)
                {
                    CftRec4(n, a, nw, w);
                }
                else if (n > 128)
                {
                    CftLeaf(n, 1, a, nw, w);
                }
                else
                {
                    CftFx41(n, a, nw, w);
                }
                BitRv2(n, ip, a);
            }
            else if (n == 32)
            {
                CftF161(a, &w[nw - 8]);
                BitRv216(a);
            }
            else
            {
                CftF081(a, w);
                BitRv208(a);
            }
        }
        else if (n == 8)
        {
            CftF040(a);
        }
        else if (n == 4)
        {
            CftX020(a);
        }
    }

    private static void CftBSub(int n, double* a, int* ip, int nw, double* w)
    {
        if (n > 8)
        {
            if (n > 32)
            {
                CftB1st(n, a, &w[nw - (n >> 2)]);
                if (n > 512)
                {
                    CftRec4(n, a, nw, w);
                }
                else if (n > 128)
                {
                    CftLeaf(n, 1, a, nw, w);
                }
                else
                {
                    CftFx41(n, a, nw, w);
                }
                BitRv2Conj(n, ip, a);
            }
            else if (n == 32)
            {
                CftF161(a, &w[nw - 8]);
                BitRv216Neg(a);
            }
            else
            {
                CftF081(a, w);
                BitRv208Neg(a);
            }
        }
        else if (n == 8)
        {
            CftB040(a);
        }
        else if (n == 4)
        {
            CftX020(a);
        }
    }
}
