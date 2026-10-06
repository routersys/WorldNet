using System.Buffers.Binary;

namespace WorldNet;

public static class WaveFile
{
    private const int ExtensibleFormatTag = 0xFFFE;

    public static int GetLength(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        using FileStream stream = File.OpenRead(path);
        ReadHeader(stream, out _, out int bitDepth, out long dataBytes);
        return checked((int)(dataBytes / (bitDepth / 8)));
    }

    public static int Read(string path, Span<double> destination, out int sampleRate,
        out int bitDepth)
    {
        ArgumentNullException.ThrowIfNull(path);

        using FileStream stream = File.OpenRead(path);
        ReadHeader(stream, out sampleRate, out bitDepth, out long dataBytes);

        int quantizationByte = bitDepth / 8;
        int length = checked((int)(dataBytes / quantizationByte));

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
            if (quantizationByte == 1)
            {
                for (int i = 0; i < count; ++i)
                {
                    destination[written + i] = (block[i] - zeroLine) / zeroLine;
                }
                written += count;
                continue;
            }

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

    private static void ReadHeader(FileStream stream, out int sampleRate, out int bitDepth,
        out long dataBytes)
    {
        Span<byte> header = stackalloc byte[8];

        ReadExactly(stream, header[..4]);
        Expect(header[..4].SequenceEqual("RIFF"u8), "RIFF");

        stream.Seek(4, SeekOrigin.Current);
        ReadExactly(stream, header[..4]);
        Expect(header[..4].SequenceEqual("WAVE"u8), "WAVE");

        sampleRate = 0;
        bitDepth = 0;
        bool formatFound = false;
        while (TryReadChunkHeader(stream, header))
        {
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(header[4..]);
            if (header[..4].SequenceEqual("fmt "u8))
            {
                Expect(size >= 16, "fmt size");
                ReadFormat(stream, size, out sampleRate, out bitDepth);
                formatFound = true;
            }
            else if (header[..4].SequenceEqual("data"u8))
            {
                Expect(formatFound, "fmt chunk before the data chunk");
                long remaining = stream.Length - stream.Position;
                dataBytes = size == uint.MaxValue || size > remaining ? remaining : size;
                return;
            }
            else
            {
                SkipChunk(stream, size, 0);
            }
        }

        throw new InvalidDataException("The data chunk was not found.");
    }

    private static void ReadFormat(FileStream stream, uint size, out int sampleRate,
        out int bitDepth)
    {
        Span<byte> field = stackalloc byte[24];
        ReadExactly(stream, field[..16]);
        int formatTag = BinaryPrimitives.ReadUInt16LittleEndian(field);
        int channels = BinaryPrimitives.ReadUInt16LittleEndian(field[2..]);
        sampleRate = BinaryPrimitives.ReadInt32LittleEndian(field[4..]);
        bitDepth = BinaryPrimitives.ReadUInt16LittleEndian(field[14..]);
        uint consumed = 16;

        if (formatTag == ExtensibleFormatTag)
        {
            Expect(size >= 40, "fmt size");
            ReadExactly(stream, field);
            formatTag = BinaryPrimitives.ReadUInt16LittleEndian(field[8..]);
            consumed = 40;
        }

        Expect(formatTag == 1, "format identifier");
        Expect(channels == 1, "monaural channel count");
        Expect(bitDepth is 8 or 16 or 24 or 32, "bit depth");
        SkipChunk(stream, size, consumed);
    }

    private static bool TryReadChunkHeader(FileStream stream, Span<byte> header)
    {
        int total = 0;
        while (total < header.Length)
        {
            int read = stream.Read(header[total..]);
            if (read == 0)
            {
                return false;
            }
            total += read;
        }
        return true;
    }

    private static void SkipChunk(FileStream stream, uint size, uint consumed)
    {
        stream.Seek((long)(size - consumed) + (size & 1), SeekOrigin.Current);
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
