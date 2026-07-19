using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace WorldNet;

public static class ParameterFile
{
    public static void WriteF0(string path, double framePeriod,
        ReadOnlySpan<double> temporalPositions, ReadOnlySpan<double> f0, bool asText)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (asText)
        {
            if (temporalPositions.Length < f0.Length)
            {
                throw new ArgumentException(
                    "The temporal positions are shorter than the F0 contour.",
                    nameof(temporalPositions));
            }

            using StreamWriter writer = new(path, false, Encoding.ASCII);
            for (int i = 0; i < f0.Length; ++i)
            {
                writer.Write(temporalPositions[i].ToString("F5", CultureInfo.InvariantCulture));
                writer.Write(' ');
                writer.Write(f0[i].ToString("F5", CultureInfo.InvariantCulture));
                writer.Write("\r\n");
            }
            return;
        }

        using FileStream stream = File.Create(path);
        WriteTag(stream, "F0  "u8);
        WriteIntParameter(stream, "NOF "u8, f0.Length);
        WriteDoubleParameter(stream, "FP  "u8, framePeriod);
        WriteDoubles(stream, f0);
    }

    public static int ReadF0(string path, Span<double> temporalPositions, Span<double> f0)
    {
        ArgumentNullException.ThrowIfNull(path);

        using FileStream stream = File.OpenRead(path);
        CheckHeader(stream, "F0  "u8);

        Span<byte> field = stackalloc byte[8];
        ReadExactly(stream, field[..4]);
        ReadExactly(stream, field[..4]);
        int numberOfFrames = BinaryPrimitives.ReadInt32LittleEndian(field);

        ReadExactly(stream, field[..4]);
        ReadExactly(stream, field);
        double framePeriod = BitConverter.Int64BitsToDouble(
            BinaryPrimitives.ReadInt64LittleEndian(field));

        if (f0.Length < numberOfFrames || temporalPositions.Length < numberOfFrames)
        {
            throw new ArgumentException(
                $"The destinations require at least {numberOfFrames} elements.", nameof(f0));
        }

        ReadDoubles(stream, f0[..numberOfFrames]);

        for (int i = 0; i < numberOfFrames; ++i)
        {
            temporalPositions[i] = i / 1000.0 * framePeriod;
        }
        return numberOfFrames;
    }

    public static double GetHeaderInformation(string path, string parameter)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(parameter);

        Span<byte> wanted = stackalloc byte[4];
        Encoding.ASCII.GetBytes(parameter.AsSpan(), wanted);

        using FileStream stream = File.OpenRead(path);
        Span<byte> field = stackalloc byte[8];
        for (int i = 0; i < 13; ++i)
        {
            ReadExactly(stream, field[..4]);
            if (!field[..4].SequenceEqual(wanted))
            {
                continue;
            }
            if (wanted.SequenceEqual("FP  "u8))
            {
                ReadExactly(stream, field);
                return BitConverter.Int64BitsToDouble(
                    BinaryPrimitives.ReadInt64LittleEndian(field));
            }
            ReadExactly(stream, field[..4]);
            return BinaryPrimitives.ReadInt32LittleEndian(field);
        }
        return 0;
    }

    public static void WriteSpectralEnvelope(string path, int fs, int f0Length,
        double framePeriod, int fftSize, int numberOfDimensions,
        ReadOnlySpan<double> spectrogram)
    {
        WriteSpectrumFile(path, "SPEC"u8, fs, f0Length, framePeriod, fftSize,
            numberOfDimensions, spectrogram);
    }

    public static int ReadSpectralEnvelope(string path, Span<double> spectrogram,
        out int fftSize, out int numberOfDimensions)
    {
        return ReadSpectrumFile(path, "SPEC"u8, spectrogram, out fftSize,
            out numberOfDimensions);
    }

    public static void WriteAperiodicity(string path, int fs, int f0Length, double framePeriod,
        int fftSize, int numberOfDimensions, ReadOnlySpan<double> aperiodicity)
    {
        WriteSpectrumFile(path, "AP  "u8, fs, f0Length, framePeriod, fftSize,
            numberOfDimensions, aperiodicity);
    }

    public static int ReadAperiodicity(string path, Span<double> aperiodicity, out int fftSize,
        out int numberOfDimensions)
    {
        return ReadSpectrumFile(path, "AP  "u8, aperiodicity, out fftSize,
            out numberOfDimensions);
    }

    private static void WriteSpectrumFile(string path, ReadOnlySpan<byte> tag, int fs,
        int f0Length, double framePeriod, int fftSize, int numberOfDimensions,
        ReadOnlySpan<double> data)
    {
        ArgumentNullException.ThrowIfNull(path);

        int dimensions = numberOfDimensions == 0 ? (fftSize / 2) + 1 : numberOfDimensions;
        if (data.Length < (long)f0Length * dimensions)
        {
            throw new ArgumentException(
                $"The data requires at least {(long)f0Length * dimensions} elements.",
                nameof(data));
        }

        using FileStream stream = File.Create(path);
        WriteTag(stream, tag);
        WriteIntParameter(stream, "NOF "u8, f0Length);
        WriteDoubleParameter(stream, "FP  "u8, framePeriod);
        WriteIntParameter(stream, "FFT "u8, fftSize);
        WriteIntParameter(stream, "NOD "u8, numberOfDimensions);
        WriteIntParameter(stream, "FS  "u8, fs);

        WriteDoubles(stream, data[..(f0Length * dimensions)]);
    }

    private static int ReadSpectrumFile(string path, ReadOnlySpan<byte> tag, Span<double> data,
        out int fftSize, out int numberOfDimensions)
    {
        ArgumentNullException.ThrowIfNull(path);

        using FileStream stream = File.OpenRead(path);
        CheckHeader(stream, tag);

        Span<byte> field = stackalloc byte[12];
        ReadExactly(stream, field[..4]);
        ReadExactly(stream, field[..4]);
        int numberOfFrames = BinaryPrimitives.ReadInt32LittleEndian(field);

        ReadExactly(stream, field);

        ReadExactly(stream, field[..4]);
        ReadExactly(stream, field[..4]);
        fftSize = BinaryPrimitives.ReadInt32LittleEndian(field);

        ReadExactly(stream, field[..4]);
        ReadExactly(stream, field[..4]);
        numberOfDimensions = BinaryPrimitives.ReadInt32LittleEndian(field);
        if (numberOfDimensions == 0)
        {
            numberOfDimensions = (fftSize / 2) + 1;
        }

        ReadExactly(stream, field[..8]);

        long required = (long)numberOfFrames * numberOfDimensions;
        if (data.Length < required)
        {
            throw new ArgumentException($"The destination requires at least {required} elements.",
                nameof(data));
        }

        ReadDoubles(stream, data[..(int)required]);
        return numberOfFrames;
    }

    private static void WriteTag(FileStream stream, ReadOnlySpan<byte> tag)
    {
        stream.Write(tag);
    }

    private static void WriteIntParameter(FileStream stream, ReadOnlySpan<byte> tag, int value)
    {
        stream.Write(tag);
        Span<byte> field = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(field, value);
        stream.Write(field);
    }

    private static void WriteDoubleParameter(FileStream stream, ReadOnlySpan<byte> tag,
        double value)
    {
        stream.Write(tag);
        Span<byte> field = stackalloc byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(field, BitConverter.DoubleToInt64Bits(value));
        stream.Write(field);
    }

    private static void WriteDoubles(FileStream stream, ReadOnlySpan<double> values)
    {
        Span<byte> chunk = stackalloc byte[4096];
        int perChunk = chunk.Length / 8;
        int written = 0;
        while (written < values.Length)
        {
            int count = Math.Min(perChunk, values.Length - written);
            for (int i = 0; i < count; ++i)
            {
                BinaryPrimitives.WriteInt64LittleEndian(chunk[(i * 8)..],
                    BitConverter.DoubleToInt64Bits(values[written + i]));
            }
            stream.Write(chunk[..(count * 8)]);
            written += count;
        }
    }

    private static void ReadDoubles(FileStream stream, Span<double> values)
    {
        Span<byte> chunk = stackalloc byte[4096];
        int perChunk = chunk.Length / 8;
        int read = 0;
        while (read < values.Length)
        {
            int count = Math.Min(perChunk, values.Length - read);
            Span<byte> block = chunk[..(count * 8)];
            ReadExactly(stream, block);
            for (int i = 0; i < count; ++i)
            {
                values[read + i] = BitConverter.Int64BitsToDouble(
                    BinaryPrimitives.ReadInt64LittleEndian(block[(i * 8)..]));
            }
            read += count;
        }
    }

    private static void CheckHeader(FileStream stream, ReadOnlySpan<byte> expected)
    {
        Span<byte> field = stackalloc byte[4];
        ReadExactly(stream, field);
        if (!field.SequenceEqual(expected))
        {
            throw new InvalidDataException("The parameter file header is malformed.");
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
}
