using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace WorldNet.Tests;

public unsafe class FftVectorPathTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    public void EveryVectorPathMatchesTheScalarPath(int log)
    {
        FftVectorPath original = OouraFft.VectorPath;
        try
        {
            int n = 1 << log;
            double[] input = CreateInput(2 * n, log);
            foreach (int sign in new[] { 1, -1 })
            {
                double[] expectedReal = RunReal(FftVectorPath.Scalar, n, sign, input);
                double[] expectedComplex = RunComplex(FftVectorPath.Scalar, n, sign, input);
                foreach (FftVectorPath path in AvailablePaths())
                {
                    AssertEqual(expectedReal, RunReal(path, n, sign, input));
                    AssertEqual(expectedComplex, RunComplex(path, n, sign, input));
                }
            }
        }
        finally
        {
            OouraFft.VectorPath = original;
        }
    }

    private static IEnumerable<FftVectorPath> AvailablePaths()
    {
        if (Avx2.IsSupported)
        {
            yield return FftVectorPath.Wide;
        }
        if (Vector128.IsHardwareAccelerated)
        {
            yield return FftVectorPath.Narrow;
        }
    }

    private static double[] RunReal(FftVectorPath path, int n, int sign, double[] input)
    {
        OouraFft.VectorPath = path;
        int* ip = (int*)NativeMemory.AllocZeroed((nuint)(2 + (int)Math.Sqrt(n / 2.0) + 64),
            sizeof(int));
        double* w = (double*)NativeMemory.AllocZeroed((nuint)(n + 64), sizeof(double));
        double* a = (double*)NativeMemory.AllocZeroed((nuint)(n + 64), sizeof(double));
        try
        {
            OouraFft.MakeWt(n >> 2, ip, w);
            OouraFft.MakeCt(n >> 2, ip, w + (n >> 2));
            for (int i = 0; i < n; ++i)
            {
                a[i] = input[i];
            }
            OouraFft.Rdft(n, sign, a, ip, w);
            return new ReadOnlySpan<double>(a, n).ToArray();
        }
        finally
        {
            NativeMemory.Free(ip);
            NativeMemory.Free(w);
            NativeMemory.Free(a);
        }
    }

    private static double[] RunComplex(FftVectorPath path, int n, int sign, double[] input)
    {
        OouraFft.VectorPath = path;
        int* ip = (int*)NativeMemory.AllocZeroed((nuint)(2 + (int)Math.Sqrt(n) + 64),
            sizeof(int));
        double* w = (double*)NativeMemory.AllocZeroed((nuint)(n + 64), sizeof(double));
        double* a = (double*)NativeMemory.AllocZeroed((nuint)((2 * n) + 64), sizeof(double));
        try
        {
            OouraFft.MakeWt(n >> 1, ip, w);
            for (int i = 0; i < 2 * n; ++i)
            {
                a[i] = input[i];
            }
            OouraFft.Cdft(2 * n, sign, a, ip, w);
            return new ReadOnlySpan<double>(a, 2 * n).ToArray();
        }
        finally
        {
            NativeMemory.Free(ip);
            NativeMemory.Free(w);
            NativeMemory.Free(a);
        }
    }

    private static double[] CreateInput(int count, int seed)
    {
        Random random = new(seed);
        double[] values = new double[count];
        for (int i = 0; i < count; ++i)
        {
            int selector = random.Next(40);
            values[i] = selector switch
            {
                0 => 0.0,
                1 => -0.0,
                _ => (random.NextDouble() * 2.0) - 1.0,
            };
        }
        return values;
    }

    private static void AssertEqual(double[] expected, double[] actual)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (int i = 0; i < expected.Length; ++i)
        {
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected[i]),
                BitConverter.DoubleToInt64Bits(actual[i]));
        }
    }
}
