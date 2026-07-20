using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace WorldNet;

internal static unsafe class MatlabFunctions
{
    internal const int DecimateFactorLength = 9;

    private const byte GatherScale = sizeof(double);

    public static void FftShift(double* x, int xLength, double* y)
    {
        for (int i = 0; i < xLength / 2; ++i)
        {
            y[i] = x[i + xLength / 2];
            y[i + xLength / 2] = x[i];
        }
    }

    public static void Histc(double* x, int xLength, double* edges, int edgesLength, int* index)
    {
        int count = 1;

        int i = 0;
        for (; i < edgesLength; ++i)
        {
            index[i] = 1;
            if (edges[i] >= x[0])
            {
                break;
            }
        }
        for (; i < edgesLength; ++i)
        {
            if (edges[i] < x[count])
            {
                index[i] = count;
            }
            else
            {
                index[i--] = count++;
            }
            if (count == xLength)
            {
                break;
            }
        }
        count--;
        for (i++; i < edgesLength; ++i)
        {
            index[i] = count;
        }
    }

    public static void Interp1(double* x, double* y, int xLength, double* xi, int xiLength,
        double* yi, in Interp1Scratch scratch)
    {
        double* h = scratch.H;
        int* k = scratch.K;

        for (int i = 0; i < xLength - 1; ++i)
        {
            h[i] = x[i + 1] - x[i];
        }
        for (int i = 0; i < xiLength; ++i)
        {
            k[i] = 0;
        }

        Histc(x, xLength, xi, xiLength, k);

        for (int i = 0; i < xiLength; ++i)
        {
            double s = (xi[i] - x[k[i] - 1]) / h[k[i] - 1];
            yi[i] = y[k[i] - 1] + s * (y[k[i]] - y[k[i] - 1]);
        }
    }

    public static int GetDecimateOutputLength(int xLength, int r)
    {
        int nout = ((xLength - 1) / r) + 1;
        int nbeg = r - (r * nout) + xLength;
        int count = 0;
        for (int i = nbeg; i < xLength + DecimateFactorLength; i += r)
        {
            ++count;
        }
        return count;
    }

    public static void Decimate(double* x, int xLength, int r, double* y,
        in DecimateScratch scratch)
    {
        const int nFact = DecimateFactorLength;
        double* tmp1 = scratch.Tmp1;
        double* tmp2 = scratch.Tmp2;

        for (int i = 0; i < nFact; ++i)
        {
            tmp1[i] = (2 * x[0]) - x[nFact - i];
        }
        for (int i = nFact; i < nFact + xLength; ++i)
        {
            tmp1[i] = x[i - nFact];
        }
        for (int i = nFact + xLength; i < (2 * nFact) + xLength; ++i)
        {
            tmp1[i] = (2 * x[xLength - 1]) - x[xLength - 2 - (i - (nFact + xLength))];
        }

        FilterForDecimate(tmp1, (2 * nFact) + xLength, r, tmp2);
        for (int i = 0; i < (2 * nFact) + xLength; ++i)
        {
            tmp1[i] = tmp2[(2 * nFact) + xLength - i - 1];
        }
        FilterForDecimate(tmp1, (2 * nFact) + xLength, r, tmp2);
        for (int i = 0; i < (2 * nFact) + xLength; ++i)
        {
            tmp1[i] = tmp2[(2 * nFact) + xLength - i - 1];
        }

        int nout = ((xLength - 1) / r) + 1;
        int nbeg = r - (r * nout) + xLength;

        int count = 0;
        for (int i = nbeg; i < xLength + nFact; i += r)
        {
            y[count++] = tmp1[i + nFact - 1];
        }
    }

    public static int MatlabRound(double x)
    {
        return x > 0 ? (int)(x + 0.5) : (int)(x - 0.5);
    }

    public static void Diff(double* x, int xLength, double* y)
    {
        for (int i = 0; i < xLength - 1; ++i)
        {
            y[i] = x[i + 1] - x[i];
        }
    }

    public static void Interp1Q(double x, double shift, double* y, int xLength, double* xi,
        int xiLength, double* yi, in Interp1QScratch scratch)
    {
        double* xiFraction = scratch.XiFraction;
        double* deltaY = scratch.DeltaY;
        int* xiBase = scratch.XiBase;

        double deltaX = shift;
        for (int i = 0; i < xiLength; ++i)
        {
            xiBase[i] = (int)((xi[i] - x) / deltaX);
            xiFraction[i] = ((xi[i] - x) / deltaX) - xiBase[i];
        }
        Diff(y, xLength, deltaY);
        deltaY[xLength - 1] = 0.0;

        InterpolateAtIndices(y, deltaY, xiFraction, xiBase, xiLength, yi);
    }

    public static void FastFftFilt(double* x, int xLength, double* h, int hLength, int fftSize,
        in ForwardRealFft forwardRealFft, in InverseRealFft inverseRealFft, double* y,
        in FastFftFiltScratch scratch)
    {
        FftComplex* xSpectrum = scratch.XSpectrum;

        for (int i = 0; i < xLength; ++i)
        {
            forwardRealFft.Waveform[i] = x[i] / fftSize;
        }
        for (int i = xLength; i < fftSize; ++i)
        {
            forwardRealFft.Waveform[i] = 0.0;
        }
        forwardRealFft.ForwardFft.Execute();
        for (int i = 0; i <= fftSize / 2; ++i)
        {
            xSpectrum[i].Real = forwardRealFft.Spectrum[i].Real;
            xSpectrum[i].Imaginary = forwardRealFft.Spectrum[i].Imaginary;
        }

        for (int i = 0; i < hLength; ++i)
        {
            forwardRealFft.Waveform[i] = h[i] / fftSize;
        }
        for (int i = hLength; i < fftSize; ++i)
        {
            forwardRealFft.Waveform[i] = 0.0;
        }
        forwardRealFft.ForwardFft.Execute();

        for (int i = 0; i <= fftSize / 2; ++i)
        {
            inverseRealFft.Spectrum[i].Real =
                (xSpectrum[i].Real * forwardRealFft.Spectrum[i].Real)
                - (xSpectrum[i].Imaginary * forwardRealFft.Spectrum[i].Imaginary);
            inverseRealFft.Spectrum[i].Imaginary =
                (xSpectrum[i].Real * forwardRealFft.Spectrum[i].Imaginary)
                + (xSpectrum[i].Imaginary * forwardRealFft.Spectrum[i].Real);
        }
        inverseRealFft.InverseFft.Execute();

        for (int i = 0; i < fftSize; ++i)
        {
            y[i] = inverseRealFft.Waveform[i];
        }
    }

    public static double MatlabStd(double* x, int xLength)
    {
        double average = 0.0;
        for (int i = 0; i < xLength; ++i)
        {
            average += x[i];
        }
        average /= xLength;

        double s = 0.0;
        for (int i = 0; i < xLength; ++i)
        {
            s += Math.Pow(x[i] - average, 2.0);
        }
        s /= xLength - 1;

        return Math.Sqrt(s);
    }

    private static void InterpolateAtIndices(double* y, double* deltaY, double* xiFraction,
        int* xiBase, int xiLength, double* yi)
    {
        int i = 0;

        if (Avx2.IsSupported)
        {
            for (; i + 4 <= xiLength; i += 4)
            {
                Vector128<int> indices = Sse2.LoadVector128(xiBase + i);
                Avx.Store(yi + i, Avx.Add(
                    Avx2.GatherVector256(y, indices, GatherScale),
                    Avx.Multiply(
                        Avx2.GatherVector256(deltaY, indices, GatherScale),
                        Avx.LoadVector256(xiFraction + i))));
            }
        }

        for (; i < xiLength; ++i)
        {
            yi[i] = y[xiBase[i]] + (deltaY[xiBase[i]] * xiFraction[i]);
        }
    }

    private static void FilterForDecimate(double* x, int xLength, int r, double* y)
    {
        double a0;
        double a1;
        double a2;
        double b0;
        double b1;

        switch (r)
        {
            case 11:
                a0 = 2.450743295230728;
                a1 = -2.06794904601978;
                a2 = 0.59574774438332101;
                b0 = 0.0026822508007163792;
                b1 = 0.0080467524021491377;
                break;
            case 12:
                a0 = 2.4981398605924205;
                a1 = -2.1368928194784025;
                a2 = 0.62187513816221485;
                b0 = 0.0021097275904709001;
                b1 = 0.0063291827714127002;
                break;
            case 10:
                a0 = 2.3936475118069387;
                a1 = -1.9873904075111861;
                a2 = 0.5658879979027055;
                b0 = 0.0034818622251927556;
                b1 = 0.010445586675578267;
                break;
            case 9:
                a0 = 2.3236003491759578;
                a1 = -1.8921545617463598;
                a2 = 0.53148928133729068;
                b0 = 0.0046331164041389372;
                b1 = 0.013899349212416812;
                break;
            case 8:
                a0 = 2.2357462340187593;
                a1 = -1.7780899984041358;
                a2 = 0.49152555365968692;
                b0 = 0.0063522763407111993;
                b1 = 0.019056829022133598;
                break;
            case 7:
                a0 = 2.1225239019534703;
                a1 = -1.6395144861046302;
                a2 = 0.44469707800587366;
                b0 = 0.0090366882681608418;
                b1 = 0.027110064804482525;
                break;
            case 6:
                a0 = 1.9715352749512141;
                a1 = -1.4686795689225347;
                a2 = 0.3893908434965701;
                b0 = 0.013469181309343825;
                b1 = 0.040407543928031475;
                break;
            case 5:
                a0 = 1.7610939654280557;
                a1 = -1.2554914843859768;
                a2 = 0.3237186507788215;
                b0 = 0.021334858522387423;
                b1 = 0.06400457556716227;
                break;
            case 4:
                a0 = 1.4499664446880227;
                a1 = -0.98943497080950582;
                a2 = 0.24578252340690215;
                b0 = 0.036710750339322612;
                b1 = 0.11013225101796784;
                break;
            case 3:
                a0 = 0.95039378983237421;
                a1 = -0.67429146741526791;
                a2 = 0.15412211621346475;
                b0 = 0.071221945171178636;
                b1 = 0.21366583551353591;
                break;
            case 2:
                a0 = 0.041156734567757189;
                a1 = -0.42599112459189636;
                a2 = 0.041037215479961225;
                b0 = 0.16797464681802227;
                b1 = 0.50392394045406674;
                break;
            default:
                a0 = 0.0;
                a1 = 0.0;
                a2 = 0.0;
                b0 = 0.0;
                b1 = 0.0;
                break;
        }

        double w0 = 0.0;
        double w1 = 0.0;
        double w2 = 0.0;
        for (int i = 0; i < xLength; ++i)
        {
            double wt = x[i] + (a0 * w0) + (a1 * w1) + (a2 * w2);
            y[i] = (b0 * wt) + (b1 * w0) + (b1 * w1) + (b0 * w2);
            w2 = w1;
            w1 = w0;
            w0 = wt;
        }
    }
}
