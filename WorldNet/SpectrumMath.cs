using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace WorldNet;

internal static unsafe class SpectrumMath
{
    private const byte InterleavedPairOrder = 0xD8;

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void PowerSpectrum(FftComplex* spectrum, double* destination, int count)
    {
        int i = 0;
        double* source = (double*)spectrum;

        if (Avx2.IsSupported)
        {
            for (; i + 4 <= count; i += 4)
            {
                Vector256<double> low = Avx.LoadVector256(source + (i * 2));
                Vector256<double> high = Avx.LoadVector256(source + (i * 2) + 4);
                Vector256<double> pairs = Avx.HorizontalAdd(
                    Avx.Multiply(low, low), Avx.Multiply(high, high));
                Avx.Store(destination + i, Avx2.Permute4x64(pairs, InterleavedPairOrder));
            }
        }
        else if (Sse3.IsSupported)
        {
            for (; i + 2 <= count; i += 2)
            {
                Vector128<double> low = Sse2.LoadVector128(source + (i * 2));
                Vector128<double> high = Sse2.LoadVector128(source + (i * 2) + 2);
                Sse2.Store(destination + i, Sse3.HorizontalAdd(
                    Sse2.Multiply(low, low), Sse2.Multiply(high, high)));
            }
        }

        for (; i < count; ++i)
        {
            destination[i] = (spectrum[i].Real * spectrum[i].Real)
                + (spectrum[i].Imaginary * spectrum[i].Imaginary);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void PowerSpectrumAndCrossProduct(FftComplex* main, FftComplex* diff,
        double* power, double* cross, int count)
    {
        int i = 0;
        double* mainSource = (double*)main;
        double* diffSource = (double*)diff;

        if (Sse3.IsSupported)
        {
            for (; i + 2 <= count; i += 2)
            {
                Vector128<double> mainLow = Sse2.LoadVector128(mainSource + (i * 2));
                Vector128<double> mainHigh = Sse2.LoadVector128(mainSource + (i * 2) + 2);
                Sse2.Store(power + i, Sse3.HorizontalAdd(
                    Sse2.Multiply(mainLow, mainLow), Sse2.Multiply(mainHigh, mainHigh)));

                Vector128<double> diffLow = Sse2.LoadVector128(diffSource + (i * 2));
                Vector128<double> diffHigh = Sse2.LoadVector128(diffSource + (i * 2) + 2);
                Sse2.Store(cross + i, Sse3.HorizontalSubtract(
                    Sse2.Multiply(mainLow, Sse2.Shuffle(diffLow, diffLow, 1)),
                    Sse2.Multiply(mainHigh, Sse2.Shuffle(diffHigh, diffHigh, 1))));
            }
        }

        for (; i < count; ++i)
        {
            cross[i] = (main[i].Real * diff[i].Imaginary) - (main[i].Imaginary * diff[i].Real);
            power[i] = (main[i].Real * main[i].Real) + (main[i].Imaginary * main[i].Imaginary);
        }
    }
}
