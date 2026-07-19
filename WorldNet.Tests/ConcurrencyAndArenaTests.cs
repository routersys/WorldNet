using System.Runtime.InteropServices;

namespace WorldNet.Tests;

public unsafe class ConcurrencyAndArenaTests
{
    private const int SampleCount = 6000;

    private static double[] Input()
    {
        return ReferenceData.Load("input_x").Values[..SampleCount];
    }

    private static double[] RunPipeline(double[] x, int fs, WorldArena arena)
    {
        DioOption dioOption = DioOption.Default;
        int f0Length = Dio.GetSamplesForDio(fs, x.Length, dioOption.FramePeriod);
        double[] positions = new double[f0Length];
        double[] f0 = new double[f0Length];
        Dio.Estimate(x, fs, dioOption, positions, f0, arena);

        double[] refined = new double[f0Length];
        StoneMask.Refine(x, fs, positions, f0, refined, arena);

        CheapTrickOption ctOption = CheapTrickOption.Create(fs);
        int spectrumLength = (ctOption.FftSize / 2) + 1;
        double[] spectrogram = new double[f0Length * spectrumLength];
        CheapTrick.Estimate(x, fs, ctOption, positions, refined, spectrogram, arena);

        double[] aperiodicity = new double[f0Length * spectrumLength];
        D4C.Estimate(x, fs, D4COption.Default, positions, refined, ctOption.FftSize,
            aperiodicity, arena);

        int yLength = (int)((f0Length - 1) * dioOption.FramePeriod / 1000.0 * fs) + 1;
        double[] y = new double[yLength];
        Synthesis.Synthesize(refined, spectrogram, aperiodicity, ctOption.FftSize,
            dioOption.FramePeriod, fs, y, arena);
        return y;
    }

    private static void AssertBitEqual(double[] expected, double[] actual, string label)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (int i = 0; i < expected.Length; ++i)
        {
            if (BitConverter.DoubleToInt64Bits(expected[i])
                != BitConverter.DoubleToInt64Bits(actual[i]))
            {
                Assert.Fail($"{label}[{i}]: expected {expected[i]:E17} but was {actual[i]:E17}");
            }
        }
    }

    [Fact]
    public void PipelineIsDeterministicAcrossRuns()
    {
        double[] x = Input();
        int fs = (int)ReferenceData.Load("meta").Values[0];

        using WorldArena first = new();
        using WorldArena second = new();
        double[] a = RunPipeline(x, fs, first);
        double[] b = RunPipeline(x, fs, second);
        double[] c = RunPipeline(x, fs, first);

        AssertBitEqual(a, b, "fresh arena");
        AssertBitEqual(a, c, "reused arena");
    }

    [Fact]
    public void PipelineIsThreadSafeWithSeparateArenas()
    {
        double[] x = Input();
        int fs = (int)ReferenceData.Load("meta").Values[0];

        using WorldArena reference = new();
        double[] expected = RunPipeline(x, fs, reference);

        const int threads = 4;
        double[][] results = new double[threads][];
        Parallel.For(0, threads, i =>
        {
            using WorldArena arena = new();
            results[i] = RunPipeline(x, fs, arena);
        });

        for (int i = 0; i < threads; ++i)
        {
            AssertBitEqual(expected, results[i], $"thread {i}");
        }
    }

    [Fact]
    public void PipelineWorksWithCallerSuppliedArena()
    {
        double[] x = Input();
        int fs = (int)ReferenceData.Load("meta").Values[0];

        nuint required;
        double[] expected;
        using (WorldArena measuring = new())
        {
            expected = RunPipeline(x, fs, measuring);
            required = measuring.Capacity;
        }

        nuint bytes = required + 4096;
        void* buffer = NativeMemory.AlignedAlloc(bytes, 64);
        try
        {
            using WorldArena supplied = WorldArena.FromNativeMemory(buffer, bytes);
            double[] actual = RunPipeline(x, fs, supplied);
            AssertBitEqual(expected, actual, "caller supplied arena");
            Assert.Equal((nuint)0, supplied.Used);
        }
        finally
        {
            NativeMemory.AlignedFree(buffer);
        }
    }
}
