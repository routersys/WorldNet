using System.Buffers.Binary;

namespace WorldNet;

public static class WaveFile
{
    public static int GetLength(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        using FileStream stream = File.OpenRead(path);
        CheckHeader(stream);

        stream.Seek(10, SeekOrigin.Current);
        Span<byte> field = stackalloc byte[4];
        ReadExactly(stream, field[..2]);
        int bitDepth = field[0];

        SeekToData(stream);
        ReadExactly(stream, field);
        int dataBytes = BinaryPrimitives.ReadInt32LittleEndian(field);
        return dataBytes / (bitDepth / 8);
    }

    public static int Read(string path, Span<double> destination, out int sampleRate,
        out int bitDepth)
    {
        ArgumentNullException.ThrowIfNull(path);

        using FileStream stream = File.OpenRead(path);
        CheckHeader(stream);

        Span<byte> field = stackalloc byte[4];
        ReadExactly(stream, field);
        sampleRate = BinaryPrimitives.ReadInt32LittleEndian(field);

        stream.Seek(6, SeekOrigin.Current);
        ReadExactly(stream, field[..2]);
        bitDepth = field[0];

        SeekToData(stream);
        ReadExactly(stream, field);
        int dataBytes = BinaryPrimitives.ReadInt32LittleEndian(field);

        int quantizationByte = bitDepth / 8;
        int length = dataBytes / quantizationByte;

        if (destination.Length < length)
        {
            throw new ArgumentException(
                $"The destination requires at least {length} samples.", nameof(destination));
        }

        double zeroLine = Math.Pow(2.0, bitDepth - 1);
        Span<byte> chunk = stackalloc byte[4096];
        int samplesPerChunk = chunk.Length / quantizationByte;
        int written = 0;
        while (written < length)
        {
            int count = Math.Min(samplesPerChunk, length - written);
            Span<byte> block = chunk[..(count * quantizationByte)];
            ReadExactly(stream, block);
            for (int i = 0; i < count; ++i)
            {
                Span<byte> sample = block.Slice(i * quantizationByte, quantizationByte);
                double signBias = 0.0;
                double tmp = 0.0;
                if (sample[quantizationByte - 1] >= 128)
                {
                    signBias = Math.Pow(2.0, bitDepth - 1);
                    sample[quantizationByte - 1] &= 0x7F;
                }
                for (int j = quantizationByte - 1; j >= 0; --j)
                {
                    tmp = (tmp * 256.0) + sample[j];
                }
                destination[written + i] = (tmp - signBias) / zeroLine;
            }
            written += count;
        }

        return length;
    }

    public static void Write(string path, ReadOnlySpan<double> x, int sampleRate)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);

        using FileStream stream = File.Create(path);

        Span<byte> header = stackalloc byte[44];
        "RIFF"u8.CopyTo(header);
        BinaryPrimitives.WriteUInt32LittleEndian(header[4..], (uint)(36 + (x.Length * 2)));
        "WAVE"u8.CopyTo(header[8..]);
        "fmt "u8.CopyTo(header[12..]);
        BinaryPrimitives.WriteUInt32LittleEndian(header[16..], 16);
        BinaryPrimitives.WriteInt16LittleEndian(header[20..], 1);
        BinaryPrimitives.WriteInt16LittleEndian(header[22..], 1);
        BinaryPrimitives.WriteUInt32LittleEndian(header[24..], (uint)sampleRate);
        BinaryPrimitives.WriteUInt32LittleEndian(header[28..], (uint)(sampleRate * 2));
        BinaryPrimitives.WriteInt16LittleEndian(header[32..], 2);
        BinaryPrimitives.WriteInt16LittleEndian(header[34..], 16);
        "data"u8.CopyTo(header[36..]);
        BinaryPrimitives.WriteUInt32LittleEndian(header[40..], (uint)(x.Length * 2));
        stream.Write(header);

        Span<byte> chunk = stackalloc byte[4096];
        int samplesPerChunk = chunk.Length / 2;
        int written = 0;
        while (written < x.Length)
        {
            int count = Math.Min(samplesPerChunk, x.Length - written);
            for (int i = 0; i < count; ++i)
            {
                int scaled = (int)(x[written + i] * 32767);
                short value = (short)WorldMath.MaxInt(-32768, WorldMath.MinInt(32767, scaled));
                BinaryPrimitives.WriteInt16LittleEndian(chunk[(i * 2)..], value);
            }
            stream.Write(chunk[..(count * 2)]);
            written += count;
        }
    }

    private static void CheckHeader(FileStream stream)
    {
        Span<byte> field = stackalloc byte[4];

        ReadExactly(stream, field);
        Expect(field.SequenceEqual("RIFF"u8), "RIFF");

        stream.Seek(4, SeekOrigin.Current);
        ReadExactly(stream, field);
        Expect(field.SequenceEqual("WAVE"u8), "WAVE");

        ReadExactly(stream, field);
        Expect(field.SequenceEqual("fmt "u8), "fmt ");

        ReadExactly(stream, field);
        Expect(field[0] == 16 && field[1] == 0 && field[2] == 0 && field[3] == 0, "fmt size");

        ReadExactly(stream, field[..2]);
        Expect(field[0] == 1 && field[1] == 0, "format identifier");

        ReadExactly(stream, field[..2]);
        Expect(field[0] == 1 && field[1] == 0, "monaural channel count");
    }

    private static void SeekToData(FileStream stream)
    {
        Span<byte> check = stackalloc byte[4];
        while (true)
        {
            int first = stream.ReadByte();
            if (first < 0)
            {
                throw new InvalidDataException("The data chunk was not found.");
            }
            if (first != 'd')
            {
                continue;
            }

            check[0] = (byte)first;
            ReadExactly(stream, check[1..]);
            if (check.SequenceEqual("data"u8))
            {
                return;
            }
            stream.Seek(-3, SeekOrigin.Current);
        }
    }

    private static void ReadExactly(FileStream stream, Span<byte> destination)
    {
        int total = 0;
        while (total < destination.Length)
        {
            int read = stream.Read(destination[total..]);
            if (read == 0)
            {
                throw new InvalidDataException("The file ended before the expected data.");
            }
            total += read;
        }
    }

    private static void Expect(bool condition, string what)
    {
        if (!condition)
        {
            throw new InvalidDataException($"The wave header is malformed at {what}.");
        }
    }
}
