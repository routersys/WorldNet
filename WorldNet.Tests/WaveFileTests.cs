namespace WorldNet.Tests;

public class WaveFileTests
{
    private static string InputWavePath =>
        Path.Combine(ReferenceData.DataDirectory, "..", "world-src", "test", "vaiueo2d.wav");

    private static string ReferenceWavePath =>
        Path.Combine(ReferenceData.DataDirectory, "synthesis_y.wav");

    [ReferenceFact]
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

    [ReferenceFact]
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

    [ReferenceFact]
    public void ReadRejectsShortDestination()
    {
        double[] destination = new double[10];

        Assert.Throws<ArgumentException>(
            () => WaveFile.Read(InputWavePath, destination, out _, out _));
    }

    [Fact]
    public void ReadDecodesEightBitSamplesAsUnsigned()
    {
        byte[] data = [0x80, 0x80, 0xFF, 0x00, 0xC0, 0x40];

        double[] read = ReadBuilt(BuildWave(8, 1, 16, 1, data), out int bitDepth);

        Assert.Equal(8, bitDepth);
        Assert.Equal([0.0, 0.0, 127.0 / 128.0, -1.0, 0.5, -0.5], read);
    }

    [Theory]
    [InlineData(18, 1)]
    [InlineData(40, 0xFFFE)]
    public void ReadAcceptsFormatChunksLongerThanSixteenBytes(int formatSize, int formatTag)
    {
        byte[] data = [0x00, 0x40, 0x00, 0xC0];

        double[] read = ReadBuilt(BuildWave(16, 1, formatSize, formatTag, data), out _);

        Assert.Equal([0.5, -0.5], read);
    }

    [Fact]
    public void ReadSkipsOtherChunksBySizeEvenWhenTheyContainTheWordData()
    {
        byte[] data = new byte[2000];
        byte[] extra = System.Text.Encoding.ASCII.GetBytes("INFOISFTmetadata_tool");

        double[] read = ReadBuilt(BuildWave(16, 1, 16, 1, data, extra), out _);

        Assert.Equal(1000, read.Length);
    }

    [Fact]
    public void LengthIsLimitedToTheBytesThatTheFileContains()
    {
        byte[] data = new byte[100];

        double[] read = ReadBuilt(BuildWave(16, 1, 16, 1, data, dataSize: 2_000_000_000), out _);

        Assert.Equal(50, read.Length);
    }

    [Fact]
    public void StreamedDataSizeReadsToTheEndOfTheFile()
    {
        byte[] data = new byte[2000];

        double[] read =
            ReadBuilt(BuildWave(16, 1, 16, 1, data, dataSize: uint.MaxValue), out _);

        Assert.Equal(1000, read.Length);
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(4, 1, 1)]
    [InlineData(12, 1, 1)]
    [InlineData(64, 1, 1)]
    [InlineData(16, 2, 1)]
    [InlineData(32, 1, 3)]
    public void ReadRejectsFormatsThatItDoesNotSupport(int bitDepth, int channels, int formatTag)
    {
        byte[] wave = BuildWave(bitDepth, channels, 16, formatTag, new byte[16]);

        Assert.Throws<InvalidDataException>(() => ReadBuilt(wave, out _));
    }

    [Fact]
    public void ReadRejectsFileWithoutDataChunk()
    {
        byte[] wave = BuildWave(16, 1, 16, 1, []);

        Assert.Throws<InvalidDataException>(() => ReadBuilt(wave[..36], out _));
    }

    private static double[] ReadBuilt(byte[] wave, out int bitDepth)
    {
        string path = Path.Combine(Path.GetTempPath(), $"worldnet_{Guid.NewGuid():N}.wav");
        try
        {
            File.WriteAllBytes(path, wave);
            double[] samples = new double[WaveFile.GetLength(path)];
            int length = WaveFile.Read(path, samples, out _, out bitDepth);
            Assert.Equal(samples.Length, length);
            return samples;
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static byte[] BuildWave(int bitDepth, int channels, int formatSize, int formatTag,
        byte[] data, byte[]? extraChunk = null, uint? dataSize = null)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);
        writer.Write("RIFF"u8.ToArray());
        writer.Write(0);
        writer.Write("WAVE"u8.ToArray());
        writer.Write("fmt "u8.ToArray());
        writer.Write(formatSize);
        writer.Write((short)formatTag);
        writer.Write((short)channels);
        writer.Write(8000);
        writer.Write(8000 * channels * bitDepth / 8);
        writer.Write((short)(channels * bitDepth / 8));
        writer.Write((short)bitDepth);
        if (formatSize >= 18)
        {
            writer.Write((short)(formatSize - 18));
        }
        if (formatSize >= 40)
        {
            writer.Write((short)bitDepth);
            writer.Write(4);
            writer.Write((short)1);
            writer.Write(new byte[14]);
        }
        if (extraChunk is not null)
        {
            writer.Write("LIST"u8.ToArray());
            writer.Write(extraChunk.Length);
            writer.Write(extraChunk);
            if ((extraChunk.Length & 1) != 0)
            {
                writer.Write((byte)0);
            }
        }
        writer.Write("data"u8.ToArray());
        writer.Write(dataSize ?? (uint)data.Length);
        writer.Write(data);
        byte[] bytes = stream.ToArray();
        BitConverter.GetBytes(bytes.Length - 8).CopyTo(bytes, 4);
        return bytes;
    }
}
