namespace WorldNet.Tests;

public class CodecTests
{
    private const int NumberOfDimensions = 40;

    private static void AssertExact(double[] expected, double[] actual, string label)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (int i = 0; i < expected.Length; ++i)
        {
            if (BitConverter.DoubleToInt64Bits(expected[i])
                != BitConverter.DoubleToInt64Bits(actual[i]))
            {
                Assert.Fail(
                    $"{label} index {i}: expected {expected[i]:E17} but was {actual[i]:E17}");
            }
        }
    }

    private static void AssertWithinOneUlp(double[] expected, double[] actual, string label)
    {
        Assert.Equal(expected.Length, actual.Length);
        long mismatches = 0;
        long maxUlp = 0;
        for (int i = 0; i < expected.Length; ++i)
        {
            long a = BitConverter.DoubleToInt64Bits(expected[i]);
            long b = BitConverter.DoubleToInt64Bits(actual[i]);
            if (a != b)
            {
                ++mismatches;
                maxUlp = Math.Max(maxUlp, Math.Abs(a - b));
            }
        }

        if (maxUlp > 1)
        {
            Assert.Fail($"{label} maxUlp={maxUlp} exceeds the one ULP tolerance of Math.Pow.");
        }

        if (mismatches * 100 >= expected.Length)
        {
            Assert.Fail($"{label} mismatches={mismatches} of {expected.Length} exceeds 1 percent.");
        }
    }

    [Fact]
    public void CodeAperiodicityMatchesReference()
    {
        double[] meta = ReferenceData.Load("meta").Values;
        int fs = (int)meta[0];
        int fftSize = (int)meta[3];
        ReferenceArray aperiodicity = ReferenceData.Load("d4c_aperiodicity");
        ReferenceArray expected = ReferenceData.Load("codec_coded_aperiodicity");

        int f0Length = aperiodicity.Rows;
        Assert.Equal(Codec.GetNumberOfAperiodicities(fs), expected.Columns);

        double[] coded = new double[expected.Values.Length];
        using WorldArena arena = new();
        Codec.CodeAperiodicity(aperiodicity.Values, f0Length, fs, fftSize, coded, arena);

        AssertExact(expected.Values, coded, "coded aperiodicity");
    }

    [Fact]
    public void DecodeAperiodicityMatchesReferenceWithinOneUlp()
    {
        double[] meta = ReferenceData.Load("meta").Values;
        int fs = (int)meta[0];
        int fftSize = (int)meta[3];
        ReferenceArray coded = ReferenceData.Load("codec_coded_aperiodicity");
        ReferenceArray expected = ReferenceData.Load("codec_decoded_aperiodicity");

        int f0Length = coded.Rows;
        double[] decoded = new double[expected.Values.Length];
        using WorldArena arena = new();
        Codec.DecodeAperiodicity(coded.Values, f0Length, fs, fftSize, decoded, arena);

        AssertWithinOneUlp(expected.Values, decoded, "decoded aperiodicity");
    }

    [Fact]
    public void CodeSpectralEnvelopeMatchesReference()
    {
        double[] meta = ReferenceData.Load("meta").Values;
        int fs = (int)meta[0];
        int fftSize = (int)meta[3];
        ReferenceArray spectrogram = ReferenceData.Load("cheaptrick_spectrogram");
        ReferenceArray expected = ReferenceData.Load("codec_coded_spectrum");

        int f0Length = spectrogram.Rows;
        Assert.Equal(NumberOfDimensions, expected.Columns);

        double[] coded = new double[expected.Values.Length];
        using WorldArena arena = new();
        Codec.CodeSpectralEnvelope(spectrogram.Values, f0Length, fs, fftSize, NumberOfDimensions,
            coded, arena);

        AssertExact(expected.Values, coded, "coded spectrum");
    }

    [Fact]
    public void DecodeSpectralEnvelopeMatchesReference()
    {
        double[] meta = ReferenceData.Load("meta").Values;
        int fs = (int)meta[0];
        int fftSize = (int)meta[3];
        ReferenceArray coded = ReferenceData.Load("codec_coded_spectrum");
        ReferenceArray expected = ReferenceData.Load("codec_decoded_spectrum");

        int f0Length = coded.Rows;
        double[] decoded = new double[expected.Values.Length];
        using WorldArena arena = new();
        Codec.DecodeSpectralEnvelope(coded.Values, f0Length, fs, fftSize, NumberOfDimensions,
            decoded, arena);

        AssertExact(expected.Values, decoded, "decoded spectrum");
    }

    [Fact]
    public void CodeAperiodicityRejectsNullArena()
    {
        double[] aperiodicity = new double[513];

        Assert.Throws<ArgumentNullException>(
            () => Codec.CodeAperiodicity(aperiodicity, 1, 22050, 1024, new double[8], null!));
    }

    [Fact]
    public void CodeSpectralEnvelopeRejectsSmallDestination()
    {
        using WorldArena arena = new();
        double[] spectrogram = new double[2 * 513];
        double[] coded = new double[(2 * NumberOfDimensions) - 1];

        Assert.Throws<ArgumentException>(
            () => Codec.CodeSpectralEnvelope(spectrogram, 2, 22050, 1024, NumberOfDimensions,
                coded, arena));
    }
}
