namespace WorldNet.Tests;

public unsafe class SpectrumMathTests
{
    [Theory]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(64)]
    [InlineData(256)]
    [InlineData(2048)]
    [InlineData(8192)]
    public void MultiplyFilterSpectrumMatchesTheScalarLoop(int fftSize)
    {
        Random random = new(fftSize);
        FftComplex[] signal = new FftComplex[(fftSize / 2) + 1];
        FftComplex[] expected = new FftComplex[fftSize];
        for (int i = 0; i < signal.Length; ++i)
        {
            signal[i].Real = random.NextDouble() - 0.5;
            signal[i].Imaginary = random.NextDouble() - 0.5;
        }
        for (int i = 0; i < fftSize; ++i)
        {
            expected[i].Real = random.NextDouble() - 0.5;
            expected[i].Imaginary = random.NextDouble() - 0.5;
        }
        FftComplex[] actual = (FftComplex[])expected.Clone();

        MultiplyScalar(signal, expected, fftSize);
        fixed (FftComplex* signalPointer = signal)
        fixed (FftComplex* actualPointer = actual)
        {
            SpectrumMath.MultiplyFilterSpectrum(signalPointer, actualPointer, fftSize);
        }

        for (int i = 0; i < fftSize; ++i)
        {
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected[i].Real),
                BitConverter.DoubleToInt64Bits(actual[i].Real));
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected[i].Imaginary),
                BitConverter.DoubleToInt64Bits(actual[i].Imaginary));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(16)]
    [InlineData(1025)]
    public void MultiplySpectraMatchesTheScalarLoop(int count)
    {
        Random random = new(count);
        FftComplex[] left = new FftComplex[count];
        FftComplex[] right = new FftComplex[count];
        FftComplex[] expected = new FftComplex[count];
        FftComplex[] actual = new FftComplex[count];
        for (int i = 0; i < count; ++i)
        {
            left[i].Real = random.NextDouble() - 0.5;
            left[i].Imaginary = random.NextDouble() - 0.5;
            right[i].Real = random.NextDouble() - 0.5;
            right[i].Imaginary = random.NextDouble() - 0.5;
            expected[i].Real =
                (left[i].Real * right[i].Real) - (left[i].Imaginary * right[i].Imaginary);
            expected[i].Imaginary =
                (left[i].Real * right[i].Imaginary) + (left[i].Imaginary * right[i].Real);
        }

        fixed (FftComplex* leftPointer = left)
        fixed (FftComplex* rightPointer = right)
        fixed (FftComplex* actualPointer = actual)
        {
            SpectrumMath.MultiplySpectra(leftPointer, rightPointer, actualPointer, count);
        }

        for (int i = 0; i < count; ++i)
        {
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected[i].Real),
                BitConverter.DoubleToInt64Bits(actual[i].Real));
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected[i].Imaginary),
                BitConverter.DoubleToInt64Bits(actual[i].Imaginary));
        }
    }

    private static void MultiplyScalar(FftComplex[] signal, FftComplex[] filter, int fftSize)
    {
        double tmp = (signal[0].Real * filter[0].Real)
            - (signal[0].Imaginary * filter[0].Imaginary);
        filter[0].Imaginary = (signal[0].Real * filter[0].Imaginary)
            + (signal[0].Imaginary * filter[0].Real);
        filter[0].Real = tmp;
        for (int i = 1; i <= fftSize / 2; ++i)
        {
            tmp = (signal[i].Real * filter[i].Real)
                - (signal[i].Imaginary * filter[i].Imaginary);
            filter[i].Imaginary = (signal[i].Real * filter[i].Imaginary)
                + (signal[i].Imaginary * filter[i].Real);
            filter[i].Real = tmp;
            filter[fftSize - i - 1].Real = filter[i].Real;
            filter[fftSize - i - 1].Imaginary = filter[i].Imaginary;
        }
    }
}
