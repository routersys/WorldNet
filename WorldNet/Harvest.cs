namespace WorldNet;

public static unsafe partial class Harvest
{
    private const int OverlapParameter = 7;

    public static int GetSamplesForHarvest(int fs, int xLength, double framePeriod)
    {
        return (int)(1000.0 * xLength / fs / framePeriod) + 1;
    }

    public static void Estimate(ReadOnlySpan<double> x, int fs, HarvestOption option,
        Span<double> temporalPositions, Span<double> f0, WorldArena arena)
    {
        ArgumentNullException.ThrowIfNull(arena);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fs);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(option.FramePeriod);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(option.F0Floor);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(option.F0Ceil);

        if (x.IsEmpty)
        {
            throw new ArgumentException("The waveform must not be empty.", nameof(x));
        }

        int f0Length = GetSamplesForHarvest(fs, x.Length, option.FramePeriod);

        if (temporalPositions.Length < f0Length)
        {
            throw new ArgumentException(
                "The temporal positions destination is shorter than the F0 contour.",
                nameof(temporalPositions));
        }

        if (f0.Length < f0Length)
        {
            throw new ArgumentException(
                "The F0 destination is shorter than the F0 contour.", nameof(f0));
        }

        fixed (double* xPointer = x)
        fixed (double* positionPointer = temporalPositions)
        fixed (double* f0Pointer = f0)
        {
            HarvestBody(xPointer, x.Length, fs, option.F0Floor, option.F0Ceil, option.FramePeriod,
                positionPointer, f0Pointer, arena);
        }
    }

    private static void HarvestBody(double* x, int xLength, int fs, double f0Floor, double f0Ceil,
        double framePeriod, double* temporalPositions, double* f0, WorldArena arena)
    {
        double targetFs = 8000.0;
        int dimensionRatio = MatlabFunctions.MatlabRound(fs / targetFs);
        double channelsInOctave = 40;

        if (framePeriod == 1.0)
        {
            HarvestGeneralBody(x, xLength, fs, 1, f0Floor, f0Ceil, channelsInOctave,
                dimensionRatio, temporalPositions, f0, arena);
            return;
        }

        int basicFramePeriod = 1;
        int basicF0Length = GetSamplesForHarvest(fs, xLength, basicFramePeriod);
        using WorldArenaScope scope = arena.BeginScope();
        double* basicF0 = (double*)arena.AllocateRaw(basicF0Length, sizeof(double));
        double* basicTemporalPositions = (double*)arena.AllocateRaw(basicF0Length, sizeof(double));
        HarvestGeneralBody(x, xLength, fs, basicFramePeriod, f0Floor, f0Ceil, channelsInOctave,
            dimensionRatio, basicTemporalPositions, basicF0, arena);

        int f0Length = GetSamplesForHarvest(fs, xLength, framePeriod);
        for (int i = 0; i < f0Length; ++i)
        {
            temporalPositions[i] = i * framePeriod / 1000.0;
            f0[i] = basicF0[WorldMath.MinInt(basicF0Length - 1,
                MatlabFunctions.MatlabRound(temporalPositions[i] * 1000.0))];
        }
    }

    private static int HarvestGeneralBodySub(double* boundaryF0List, int numberOfChannels,
        int f0Length, double actualFs, int yLength, double* temporalPositions, int fftSize,
        double f0Floor, double f0Ceil, int maxCandidates, double** f0Candidates,
        in HarvestScratch scratch)
    {
        double** rawF0Candidates = scratch.RawF0Candidates;

        GetRawF0Candidates(boundaryF0List, numberOfChannels, actualFs, yLength, temporalPositions,
            f0Length, fftSize, f0Floor, f0Ceil, rawF0Candidates, scratch);

        int numberOfCandidates = DetectOfficialF0Candidates(rawF0Candidates, numberOfChannels,
            f0Length, maxCandidates, f0Candidates, scratch);

        if (numberOfCandidates * OverlapParameter > maxCandidates)
        {
            throw new InvalidOperationException(
                $"The overlap stage needs {numberOfCandidates * OverlapParameter} columns " +
                $"but only {maxCandidates} are reserved.");
        }

        OverlapF0Candidates(f0Length, numberOfCandidates, f0Candidates);

        return numberOfCandidates;
    }

    private static void HarvestGeneralBody(double* x, int xLength, int fs, int framePeriod,
        double f0Floor, double f0Ceil, double channelsInOctave, int speed,
        double* temporalPositions, double* f0, WorldArena arena)
    {
        using WorldArenaScope scope = arena.BeginScope();

        double adjustedF0Floor = f0Floor * 0.9;
        double adjustedF0Ceil = f0Ceil * 1.1;
        int numberOfChannels = 1 + (int)(Math.Log(adjustedF0Ceil / adjustedF0Floor) /
            WorldConstants.Log2 * channelsInOctave);
        double* boundaryF0List = (double*)arena.AllocateRaw(numberOfChannels, sizeof(double));
        for (int i = 0; i < numberOfChannels; ++i)
        {
            boundaryF0List[i] = adjustedF0Floor * Math.Pow(2.0, (i + 1) / channelsInOctave);
        }

        int decimationRatio = WorldMath.MaxInt(WorldMath.MinInt(speed, 12), 1);
        int yLength = (int)Math.Ceiling((double)xLength / decimationRatio);
        double actualFs = (double)fs / decimationRatio;
        int fftSize = Common.GetSuitableFftSize(yLength + 5 +
            (2 * (int)(2.0 * actualFs / boundaryF0List[0])));

        int f0Length = GetSamplesForHarvest(fs, xLength, framePeriod);

        int lag = (int)(Math.Ceiling(140.0 / decimationRatio) * decimationRatio);
        int newXLength = xLength + (lag * 2);
        int maxCandidates =
            MatlabFunctions.MatlabRound(numberOfChannels / 10.0) * OverlapParameter;

        HarvestScratch scratch = HarvestScratch.Bind(arena, numberOfChannels, newXLength, yLength,
            f0Length, maxCandidates, fftSize);

        GetWaveformAndSpectrum(x, xLength, yLength, fftSize, decimationRatio, scratch);

        for (int i = 0; i < f0Length; ++i)
        {
            temporalPositions[i] = i * framePeriod / 1000.0;
            f0[i] = 0.0;
        }

        int numberOfCandidates = HarvestGeneralBodySub(boundaryF0List, numberOfChannels, f0Length,
            actualFs, yLength, temporalPositions, fftSize, f0Floor, f0Ceil, maxCandidates,
            scratch.F0Candidates, scratch) * OverlapParameter;

        RefineF0Candidates(scratch.Y, yLength, actualFs, temporalPositions, f0Length,
            numberOfCandidates, f0Floor, f0Ceil, scratch.F0Candidates, scratch.F0CandidatesScore,
            arena);
        RemoveUnreliableCandidates(f0Length, numberOfCandidates, scratch.F0Candidates,
            scratch.F0CandidatesScore, arena);

        double* bestF0Contour = scratch.BestF0Contour;
        FixF0Contour(scratch.F0Candidates, scratch.F0CandidatesScore, f0Length, numberOfCandidates,
            bestF0Contour, arena);
        SmoothF0Contour(bestF0Contour, f0Length, f0, arena);
    }
}
