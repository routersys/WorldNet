using System.Runtime.CompilerServices;

namespace WorldNet.Tests;

public class WorldSynthesizerTests
{
    private const int BufferSize = 64;

    [ReferenceFact]
    public void RealtimeSynthesisMatchesReference()
    {
        double[] meta = ReferenceData.Load("meta").Values;
        int fs = (int)meta[0];
        double framePeriod = meta[2];
        int fftSize = (int)meta[3];
        double[] f0 = ReferenceData.Load("stonemask_f0").Values;
        ReferenceArray spectrogram = ReferenceData.Load("cheaptrick_spectrogram");
        ReferenceArray aperiodicity = ReferenceData.Load("d4c_aperiodicity");
        double[] expected = ReferenceData.Load("synthesis_realtime_y").Values;

        int f0Length = f0.Length;
        int yLength = expected.Length;

        using WorldArena arena = new();
        WorldSynthesizer synthesizer =
            new(arena, fs, framePeriod, fftSize, BufferSize, 1, f0Length);

        Assert.True(synthesizer.AddParameters(f0, spectrogram.Values, aperiodicity.Values));

        double[] y = new double[yLength + (BufferSize * 2)];
        int produced = 0;
        while (synthesizer.Synthesize())
        {
            for (int j = 0; j < BufferSize; ++j)
            {
                if (produced < y.Length)
                {
                    y[produced++] = synthesizer.Buffer[j];
                }
            }
        }

        Assert.True(produced > 0);
        for (int i = produced; i < yLength; ++i)
        {
            if (expected[i] != 0.0)
            {
                Assert.Fail(
                    $"the synthesizer stopped at {produced} but the reference has " +
                    $"{expected[i]:E17} at sample {i}.");
            }
        }

        if (ReferenceData.SquaresExactly)
        {
            for (int i = 0; i < yLength; ++i)
            {
                if (BitConverter.DoubleToInt64Bits(expected[i])
                    != BitConverter.DoubleToInt64Bits(y[i]))
                {
                    Assert.Fail($"sample {i}: expected {expected[i]:E17} but was {y[i]:E17}");
                }
            }
        }
        else
        {
            double error = WaveformComparison.MaxErrorInPeakUlps(expected, y.AsSpan(0, yLength));
            if (error > WaveformComparison.PeakUlpTolerance)
            {
                Assert.Fail(
                    $"the maximum error of {error} ULP of the peak exceeds " +
                    $"{WaveformComparison.PeakUlpTolerance}, the tolerance inherited from the " +
                    "reference's pow.");
            }
        }
    }

    [Fact]
    public void ConstructorRejectsNullArena()
    {
        Assert.Throws<ArgumentNullException>(
            () => new WorldSynthesizer(null!, 22050, 5.0, 1024, 64, 1, 100));
    }

    [Fact]
    public void AddParametersRejectsOversizedChunk()
    {
        using WorldArena arena = new();
        WorldSynthesizer synthesizer = new(arena, 22050, 5.0, 1024, 64, 1, 4);
        double[] f0 = new double[5];
        double[] spectrogram = new double[5 * 513];
        double[] aperiodicity = new double[5 * 513];

        Assert.Throws<ArgumentOutOfRangeException>(
            () => synthesizer.AddParameters(f0, spectrogram, aperiodicity));
    }

    [Fact]
    public void AddParametersRejectsEmptyContour()
    {
        using WorldArena arena = new();
        WorldSynthesizer synthesizer = new(arena, 22050, 5.0, 1024, 64, 1, 4);

        Assert.Throws<ArgumentException>(() => synthesizer.AddParameters(
            ReadOnlySpan<double>.Empty, ReadOnlySpan<double>.Empty, ReadOnlySpan<double>.Empty));
    }

    [Fact]
    public void SynthesizerKeepsItsArenaAlive()
    {
        WorldSynthesizer synthesizer =
            CreateWithUnreferencedArena(out WeakReference<WorldArena> arena);

        for (int i = 0; i < 3; ++i)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        Assert.True(arena.TryGetTarget(out _));
        GC.KeepAlive(synthesizer);
    }

    [Fact]
    public void UseAfterTheArenaIsDisposedThrows()
    {
        WorldArena arena = new();
        WorldSynthesizer synthesizer = new(arena, 22050, 5.0, 1024, 64, 1, 4);
        double[] f0 = new double[4];
        double[] spectrogram = new double[4 * 513];
        double[] aperiodicity = new double[4 * 513];
        arena.Dispose();

        Assert.Throws<ObjectDisposedException>(() => synthesizer.Synthesize());
        Assert.Throws<ObjectDisposedException>(() => synthesizer.Refresh());
        Assert.Throws<ObjectDisposedException>(
            () => synthesizer.AddParameters(f0, spectrogram, aperiodicity));
        Assert.Throws<ObjectDisposedException>(() =>
        {
            _ = synthesizer.Buffer.Length;
        });
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WorldSynthesizer CreateWithUnreferencedArena(
        out WeakReference<WorldArena> arena)
    {
        WorldArena created = new();
        arena = new WeakReference<WorldArena>(created);
        return new WorldSynthesizer(created, 22050, 5.0, 1024, 64, 1, 4);
    }
}
