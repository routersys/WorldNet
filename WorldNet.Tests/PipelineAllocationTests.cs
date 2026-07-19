namespace WorldNet.Tests;

public class PipelineAllocationTests
{
    private const int SampleCount = 8000;

    private sealed class Workspace
    {
        public double[] X = [];
        public double[] TemporalPositions = [];
        public double[] F0 = [];
        public double[] RefinedF0 = [];
        public double[] Spectrogram = [];
        public double[] Aperiodicity = [];
        public double[] Y = [];
        public int Fs;
        public int FftSize;
        public double FramePeriod;
    }

    private static Workspace CreateWorkspace()
    {
        double[] source = ReferenceData.Load("input_x").Values;
        int fs = (int)ReferenceData.Load("meta").Values[0];
        DioOption dioOption = DioOption.Default;

        int f0Length = Dio.GetSamplesForDio(fs, SampleCount, dioOption.FramePeriod);
        CheapTrickOption cheapTrickOption = CheapTrickOption.Create(fs);
        int spectrumLength = (cheapTrickOption.FftSize / 2) + 1;
        int yLength = (int)((f0Length - 1) * dioOption.FramePeriod / 1000.0 * fs) + 1;

        Workspace workspace = new()
        {
            X = source[..SampleCount],
            TemporalPositions = new double[f0Length],
            F0 = new double[f0Length],
            RefinedF0 = new double[f0Length],
            Spectrogram = new double[f0Length * spectrumLength],
            Aperiodicity = new double[f0Length * spectrumLength],
            Y = new double[yLength],
            Fs = fs,
            FftSize = cheapTrickOption.FftSize,
            FramePeriod = dioOption.FramePeriod,
        };
        return workspace;
    }

    private static void RunPipeline(Workspace w, WorldArena arena)
    {
        DioOption dioOption = DioOption.Default;
        CheapTrickOption cheapTrickOption = CheapTrickOption.Create(w.Fs);

        Dio.Estimate(w.X, w.Fs, dioOption, w.TemporalPositions, w.F0, arena);
        StoneMask.Refine(w.X, w.Fs, w.TemporalPositions, w.F0, w.RefinedF0, arena);
        CheapTrick.Estimate(w.X, w.Fs, cheapTrickOption, w.TemporalPositions, w.RefinedF0,
            w.Spectrogram, arena);
        D4C.Estimate(w.X, w.Fs, D4COption.Default, w.TemporalPositions, w.RefinedF0, w.FftSize,
            w.Aperiodicity, arena);
        Synthesis.Synthesize(w.RefinedF0, w.Spectrogram, w.Aperiodicity, w.FftSize,
            w.FramePeriod, w.Fs, w.Y, arena);
    }

    [Fact]
    public void FullPipelineDoesNotAllocateManagedMemory()
    {
        Workspace workspace = CreateWorkspace();
        using WorldArena arena = new();

        for (int i = 0; i < 2; ++i)
        {
            RunPipeline(workspace, arena);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 3; ++i)
        {
            RunPipeline(workspace, arena);
        }
        long after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(0L, after - before);
    }

    [Fact]
    public void ArenaIsFullyReleasedAfterPipeline()
    {
        Workspace workspace = CreateWorkspace();
        using WorldArena arena = new();

        RunPipeline(workspace, arena);
        nuint usedAfterFirst = arena.Used;
        nuint capacityAfterFirst = arena.Capacity;

        RunPipeline(workspace, arena);

        Assert.Equal((nuint)0, usedAfterFirst);
        Assert.Equal((nuint)0, arena.Used);
        Assert.Equal(capacityAfterFirst, arena.Capacity);
    }

    [Fact]
    public void RealtimeSynthesizerDoesNotAllocateManagedMemory()
    {
        Workspace workspace = CreateWorkspace();
        using WorldArena arena = new();
        RunPipeline(workspace, arena);

        WorldSynthesizer synthesizer =
            new(arena, workspace.Fs, workspace.FramePeriod, workspace.FftSize, 64, 1,
                workspace.F0.Length);

        for (int i = 0; i < 2; ++i)
        {
            synthesizer.Refresh();
            synthesizer.AddParameters(workspace.RefinedF0, workspace.Spectrogram,
                workspace.Aperiodicity);
            while (synthesizer.Synthesize())
            {
            }
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        synthesizer.Refresh();
        synthesizer.AddParameters(workspace.RefinedF0, workspace.Spectrogram,
            workspace.Aperiodicity);
        while (synthesizer.Synthesize())
        {
        }
        long after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(0L, after - before);
    }
}
