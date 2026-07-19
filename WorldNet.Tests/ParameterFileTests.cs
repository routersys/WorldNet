namespace WorldNet.Tests;

public class ParameterFileTests
{
    private static string Reference(string name) =>
        Path.Combine(ReferenceData.DataDirectory, name);

    private static void AssertFileBytesMatch(string expectedPath, string actualPath)
    {
        byte[] expected = File.ReadAllBytes(expectedPath);
        byte[] actual = File.ReadAllBytes(actualPath);
        Assert.Equal(expected.Length, actual.Length);
        for (int i = 0; i < expected.Length; ++i)
        {
            if (expected[i] != actual[i])
            {
                Assert.Fail($"byte {i}: expected {expected[i]} but was {actual[i]}");
            }
        }
    }

    private static byte[] NormalizeLineEndings(byte[] source)
    {
        List<byte> result = new(source.Length);
        for (int i = 0; i < source.Length; ++i)
        {
            if (i + 2 < source.Length && source[i] == 13 && source[i + 1] == 13
                && source[i + 2] == 10)
            {
                continue;
            }
            result.Add(source[i]);
        }
        return [.. result];
    }

    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), $"worldnet_{Guid.NewGuid():N}.bin");

    [Fact]
    public void WriteF0BinaryMatchesReferenceBytes()
    {
        double[] meta = ReferenceData.Load("meta").Values;
        double[] positions = ReferenceData.Load("dio_temporal_positions").Values;
        double[] f0 = ReferenceData.Load("stonemask_f0").Values;

        string path = TempPath();
        try
        {
            ParameterFile.WriteF0(path, meta[2], positions, f0, false);
            AssertFileBytesMatch(Reference("param_f0.bin"), path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void WriteF0TextMatchesReferenceContent()
    {
        double[] meta = ReferenceData.Load("meta").Values;
        double[] positions = ReferenceData.Load("dio_temporal_positions").Values;
        double[] f0 = ReferenceData.Load("stonemask_f0").Values;

        string path = TempPath();
        try
        {
            ParameterFile.WriteF0(path, meta[2], positions, f0, true);
            byte[] expected = File.ReadAllBytes(Reference("param_f0.txt"));
            byte[] actual = File.ReadAllBytes(path);
            byte[] normalized = NormalizeLineEndings(expected);

            Assert.Equal(normalized.Length, actual.Length);
            for (int i = 0; i < normalized.Length; ++i)
            {
                if (normalized[i] != actual[i])
                {
                    Assert.Fail($"byte {i}: expected {normalized[i]} but was {actual[i]}");
                }
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void WriteSpectralEnvelopeMatchesReferenceBytes()
    {
        double[] meta = ReferenceData.Load("meta").Values;
        ReferenceArray spectrogram = ReferenceData.Load("cheaptrick_spectrogram");

        string path = TempPath();
        try
        {
            ParameterFile.WriteSpectralEnvelope(path, (int)meta[0], spectrogram.Rows, meta[2],
                (int)meta[3], 0, spectrogram.Values);
            AssertFileBytesMatch(Reference("param_spec.bin"), path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void WriteAperiodicityMatchesReferenceBytes()
    {
        double[] meta = ReferenceData.Load("meta").Values;
        ReferenceArray aperiodicity = ReferenceData.Load("d4c_aperiodicity");

        string path = TempPath();
        try
        {
            ParameterFile.WriteAperiodicity(path, (int)meta[0], aperiodicity.Rows, meta[2],
                (int)meta[3], 0, aperiodicity.Values);
            AssertFileBytesMatch(Reference("param_ap.bin"), path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReadF0MatchesReferenceValues()
    {
        double[] expected = ReferenceData.Load("stonemask_f0").Values;
        double[] f0 = new double[expected.Length];
        double[] positions = new double[expected.Length];

        int frames = ParameterFile.ReadF0(Reference("param_f0.bin"), positions, f0);

        Assert.Equal(expected.Length, frames);
        for (int i = 0; i < frames; ++i)
        {
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected[i]),
                BitConverter.DoubleToInt64Bits(f0[i]));
        }
    }

    [Fact]
    public void ReadSpectralEnvelopeMatchesReferenceValues()
    {
        ReferenceArray expected = ReferenceData.Load("cheaptrick_spectrogram");
        double[] spectrogram = new double[expected.Values.Length];

        int frames = ParameterFile.ReadSpectralEnvelope(Reference("param_spec.bin"), spectrogram,
            out int fftSize, out int numberOfDimensions);

        Assert.Equal(expected.Rows, frames);
        Assert.Equal(expected.Columns, numberOfDimensions);
        Assert.Equal((int)ReferenceData.Load("meta").Values[3], fftSize);
        for (int i = 0; i < expected.Values.Length; ++i)
        {
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Values[i]),
                BitConverter.DoubleToInt64Bits(spectrogram[i]));
        }
    }

    [Fact]
    public void GetHeaderInformationMatchesReference()
    {
        double[] meta = ReferenceData.Load("meta").Values;

        Assert.Equal(159, ParameterFile.GetHeaderInformation(Reference("param_spec.bin"), "NOF "));
        Assert.Equal(meta[2], ParameterFile.GetHeaderInformation(Reference("param_spec.bin"), "FP  "));
        Assert.Equal(meta[3], ParameterFile.GetHeaderInformation(Reference("param_spec.bin"), "FFT "));
        Assert.Equal(meta[0], ParameterFile.GetHeaderInformation(Reference("param_spec.bin"), "FS  "));
    }
}
