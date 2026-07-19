namespace WorldNet.Tests;

public class WaveFileTests
{
    private static string InputWavePath =>
        Path.Combine(ReferenceData.DataDirectory, "..", "world-src", "test", "vaiueo2d.wav");

    private static string ReferenceWavePath =>
        Path.Combine(ReferenceData.DataDirectory, "synthesis_y.wav");

    [Fact]
    public void ReadMatchesReferenceWaveform()
    {
        double[] expected = ReferenceData.Load("input_x").Values;
        double[] meta = ReferenceData.Load("meta").Values;

        Assert.Equal(expected.Length, WaveFile.GetLength(InputWavePath));

        double[] x = new double[expected.Length];
        int length = WaveFile.Read(InputWavePath, x, out int sampleRate, out int bitDepth);

        Assert.Equal(expected.Length, length);
        Assert.Equal((int)meta[0], sampleRate);
        Assert.Equal(16, bitDepth);

        for (int i = 0; i < length; ++i)
        {
            if (BitConverter.DoubleToInt64Bits(expected[i])
                != BitConverter.DoubleToInt64Bits(x[i]))
            {
                Assert.Fail($"sample {i}: expected {expected[i]:E17} but was {x[i]:E17}");
            }
        }
    }

    [Fact]
    public void WriteMatchesReferenceFileBytes()
    {
        double[] meta = ReferenceData.Load("meta").Values;
        int fs = (int)meta[0];
        double[] y = ReferenceData.Load("synthesis_y").Values;
        byte[] expected = File.ReadAllBytes(ReferenceWavePath);

        string path = Path.Combine(Path.GetTempPath(), $"worldnet_{Guid.NewGuid():N}.wav");
        try
        {
            WaveFile.Write(path, y, fs);
            byte[] actual = File.ReadAllBytes(path);

            Assert.Equal(expected.Length, actual.Length);
            for (int i = 0; i < expected.Length; ++i)
            {
                if (expected[i] != actual[i])
                {
                    Assert.Fail($"byte {i}: expected {expected[i]} but was {actual[i]}");
                }
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void RoundTripPreservesQuantizedSamples()
    {
        double[] source = [0.0, 0.5, -0.5, 1.0, -1.0, 0.123456789, -0.987654321];
        string path = Path.Combine(Path.GetTempPath(), $"worldnet_{Guid.NewGuid():N}.wav");
        try
        {
            WaveFile.Write(path, source, 44100);

            double[] read = new double[source.Length];
            int length = WaveFile.Read(path, read, out int sampleRate, out int bitDepth);

            Assert.Equal(source.Length, length);
            Assert.Equal(44100, sampleRate);
            Assert.Equal(16, bitDepth);
            for (int i = 0; i < length; ++i)
            {
                double quantized =
                    Math.Clamp((int)(source[i] * 32767), -32768, 32767) / 32768.0;
                Assert.Equal(quantized, read[i]);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReadRejectsShortDestination()
    {
        double[] destination = new double[10];

        Assert.Throws<ArgumentException>(
            () => WaveFile.Read(InputWavePath, destination, out _, out _));
    }
}
