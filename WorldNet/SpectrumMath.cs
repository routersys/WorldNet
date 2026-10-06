using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
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
        else if (AdvSimd.Arm64.IsSupported)
        {
            for (; i + 2 <= count; i += 2)
            {
                Vector128<double> low = AdvSimd.LoadVector128(source + (i * 2));
                Vector128<double> high = AdvSimd.LoadVector128(source + (i * 2) + 2);
                AdvSimd.Store(destination + i, AdvSimd.Arm64.AddPairwise(
                    AdvSimd.Arm64.Multiply(low, low), AdvSimd.Arm64.Multiply(high, high)));
            }
        }

        for (; i < count; ++i)
        {
            destination[i] = (spectrum[i].Real * spectrum[i].Real)
                + (spectrum[i].Imaginary * spectrum[i].Imaginary);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void SortNonNegative(double* values, double* temporary, int count)
    {
        if (count < 2)
        {
            return;
        }

        const int Radix = 256;
        const int Passes = 8;

        int* histogram = stackalloc int[Radix * Passes];
        new Span<int>(histogram, Radix * Passes).Clear();

        ulong* keys = (ulong*)values;
        for (int i = 0; i < count; ++i)
        {
            ulong key = keys[i];
            for (int pass = 0; pass < Passes; ++pass)
            {
                ++histogram[(pass * Radix) + (int)((key >> (pass * 8)) & 0xFF)];
            }
        }

        ulong* from = (ulong*)values;
        ulong* to = (ulong*)temporary;
        bool relocated = false;

        for (int pass = 0; pass < Passes; ++pass)
        {
            int* counts = histogram + (pass * Radix);
            if (counts[(int)((from[0] >> (pass * 8)) & 0xFF)] == count)
            {
                continue;
            }

            int offset = 0;
            for (int digit = 0; digit < Radix; ++digit)
            {
                int occurrences = counts[digit];
                counts[digit] = offset;
                offset += occurrences;
            }

            for (int i = 0; i < count; ++i)
            {
                ulong key = from[i];
                to[counts[(int)((key >> (pass * 8)) & 0xFF)]++] = key;
            }

            ulong* previous = from;
            from = to;
            to = previous;
            relocated = !relocated;
        }

        if (relocated)
        {
            Buffer.MemoryCopy(from, values, (long)count * sizeof(double),
                (long)count * sizeof(double));
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
        else if (AdvSimd.Arm64.IsSupported)
        {
            for (; i + 2 <= count; i += 2)
            {
                Vector128<double> mainLow = AdvSimd.LoadVector128(mainSource + (i * 2));
                Vector128<double> mainHigh = AdvSimd.LoadVector128(mainSource + (i * 2) + 2);
                Vector128<double> mainReal = AdvSimd.Arm64.UnzipEven(mainLow, mainHigh);
                Vector128<double> mainImaginary = AdvSimd.Arm64.UnzipOdd(mainLow, mainHigh);
                AdvSimd.Store(power + i, AdvSimd.Arm64.Add(
                    AdvSimd.Arm64.Multiply(mainReal, mainReal),
                    AdvSimd.Arm64.Multiply(mainImaginary, mainImaginary)));

                Vector128<double> diffLow = AdvSimd.LoadVector128(diffSource + (i * 2));
                Vector128<double> diffHigh = AdvSimd.LoadVector128(diffSource + (i * 2) + 2);
                Vector128<double> diffReal = AdvSimd.Arm64.UnzipEven(diffLow, diffHigh);
                Vector128<double> diffImaginary = AdvSimd.Arm64.UnzipOdd(diffLow, diffHigh);
                AdvSimd.Store(cross + i, AdvSimd.Arm64.Subtract(
                    AdvSimd.Arm64.Multiply(mainReal, diffImaginary),
                    AdvSimd.Arm64.Multiply(mainImaginary, diffReal)));
            }
        }

        for (; i < count; ++i)
        {
            cross[i] = (main[i].Real * diff[i].Imaginary) - (main[i].Imaginary * diff[i].Real);
            power[i] = (main[i].Real * main[i].Real) + (main[i].Imaginary * main[i].Imaginary);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void MultiplyFilterSpectrum(FftComplex* signal, FftComplex* filter,
        int fftSize)
    {
        double first = (signal[0].Real * filter[0].Real)
            - (signal[0].Imaginary * filter[0].Imaginary);
        filter[0].Imaginary = (signal[0].Real * filter[0].Imaginary)
            + (signal[0].Imaginary * filter[0].Real);
        filter[0].Real = first;

        int half = fftSize / 2;
        int i = 1;
        double* source = (double*)signal;
        double* target = (double*)filter;

        if (Avx2.IsSupported)
        {
            for (; i + 2 <= half; i += 2)
            {
                Vector256<double> value = Avx.LoadVector256(source + (i * 2));
                Vector256<double> coefficient = Avx.LoadVector256(target + (i * 2));
                Vector256<double> product = ProductAvx(value, coefficient);
                Avx.Store(target + (i * 2), product);
                Avx.Store(target + ((fftSize - i - 2) * 2), Avx.Permute2x128(product, product, 1));
            }
        }
        else if (Sse3.IsSupported)
        {
            for (; i < half; ++i)
            {
                Vector128<double> value = Sse2.LoadVector128(source + (i * 2));
                Vector128<double> coefficient = Sse2.LoadVector128(target + (i * 2));
                Vector128<double> product = ProductSse3(value, coefficient);
                Sse2.Store(target + (i * 2), product);
                Sse2.Store(target + ((fftSize - i - 1) * 2), product);
            }
        }
        else if (AdvSimd.Arm64.IsSupported)
        {
            for (; i < half; ++i)
            {
                Vector128<double> value = AdvSimd.LoadVector128(source + (i * 2));
                Vector128<double> coefficient = AdvSimd.LoadVector128(target + (i * 2));
                Vector128<double> product = ProductAdvSimd(value, coefficient);
                AdvSimd.Store(target + (i * 2), product);
                AdvSimd.Store(target + ((fftSize - i - 1) * 2), product);
            }
        }

        for (; i <= half; ++i)
        {
            double real = (signal[i].Real * filter[i].Real)
                - (signal[i].Imaginary * filter[i].Imaginary);
            filter[i].Imaginary = (signal[i].Real * filter[i].Imaginary)
                + (signal[i].Imaginary * filter[i].Real);
            filter[i].Real = real;
            filter[fftSize - i - 1].Real = filter[i].Real;
            filter[fftSize - i - 1].Imaginary = filter[i].Imaginary;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void MultiplySpectra(FftComplex* left, FftComplex* right,
        FftComplex* destination, int count)
    {
        int i = 0;
        double* leftSource = (double*)left;
        double* rightSource = (double*)right;
        double* target = (double*)destination;

        if (Avx2.IsSupported)
        {
            for (; i + 2 <= count; i += 2)
            {
                Avx.Store(target + (i * 2), ProductAvx(Avx.LoadVector256(leftSource + (i * 2)),
                    Avx.LoadVector256(rightSource + (i * 2))));
            }
        }
        else if (Sse3.IsSupported)
        {
            for (; i < count; ++i)
            {
                Sse2.Store(target + (i * 2), ProductSse3(Sse2.LoadVector128(leftSource + (i * 2)),
                    Sse2.LoadVector128(rightSource + (i * 2))));
            }
        }
        else if (AdvSimd.Arm64.IsSupported)
        {
            for (; i < count; ++i)
            {
                AdvSimd.Store(target + (i * 2),
                    ProductAdvSimd(AdvSimd.LoadVector128(leftSource + (i * 2)),
                        AdvSimd.LoadVector128(rightSource + (i * 2))));
            }
        }

        for (; i < count; ++i)
        {
            double real = (left[i].Real * right[i].Real) - (left[i].Imaginary * right[i].Imaginary);
            destination[i].Imaginary =
                (left[i].Real * right[i].Imaginary) + (left[i].Imaginary * right[i].Real);
            destination[i].Real = real;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<double> ProductAvx(Vector256<double> value,
        Vector256<double> coefficient)
    {
        return Avx.AddSubtract(
            Avx.Multiply(value, Avx.Permute(coefficient, 0x0)),
            Avx.Multiply(Avx.Permute(value, 0x5), Avx.Permute(coefficient, 0xF)));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<double> ProductSse3(Vector128<double> value,
        Vector128<double> coefficient)
    {
        return Sse3.AddSubtract(
            Sse2.Multiply(value, Sse2.Shuffle(coefficient, coefficient, 0)),
            Sse2.Multiply(Sse2.Shuffle(value, value, 1),
                Sse2.Shuffle(coefficient, coefficient, 3)));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<double> ProductAdvSimd(Vector128<double> value,
        Vector128<double> coefficient)
    {
        return AdvSimd.Arm64.Add(
            AdvSimd.Arm64.Multiply(value,
                AdvSimd.Arm64.DuplicateSelectedScalarToVector128(coefficient, 0)),
            AdvSimd.Arm64.Multiply(AdvSimd.ExtractVector128(value, value, 1),
                AdvSimd.Arm64.DuplicateSelectedScalarToVector128(coefficient, 1))
            ^ Vector128.Create(-0.0, 0.0));
    }
}
