namespace WorldNet;

public static unsafe class StoneMask
{
    public static void Refine(ReadOnlySpan<double> x, int fs,
        ReadOnlySpan<double> temporalPositions, ReadOnlySpan<double> f0,
        Span<double> refinedF0, WorldArena arena)
    {
        ArgumentNullException.ThrowIfNull(arena);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fs);

        if (x.IsEmpty)
        {
            throw new ArgumentException("The waveform must not be empty.", nameof(x));
        }

        if (temporalPositions.Length != f0.Length)
        {
            throw new ArgumentException(
                "The temporal positions and the F0 contour must have the same length.",
                nameof(temporalPositions));
        }

        if (refinedF0.Length < f0.Length)
        {
            throw new ArgumentException(
                "The destination is shorter than the F0 contour.", nameof(refinedF0));
        }

        if (f0.IsEmpty)
        {
            return;
        }

        fixed (double* xPointer = x)
        fixed (double* positionPointer = temporalPositions)
        fixed (double* f0Pointer = f0)
        fixed (double* refinedPointer = refinedF0)
        {
            for (int i = 0; i < f0.Length; i++)
            {
                refinedPointer[i] = GetRefinedF0(
                    xPointer, x.Length, fs, positionPointer[i], f0Pointer[i], arena);
            }
        }
    }

    private static double GetRefinedF0(double* x, int xLength, int fs, double currentPosition,
        double initialF0, WorldArena arena)
    {
        if (initialF0 <= WorldConstants.FloorF0StoneMask || initialF0 > fs / 12.0)
        {
            return 0.0;
        }

        int halfWindowLength = (int)((1.5 * fs / initialF0) + 1.0);
        double windowLengthInTime = ((2.0 * halfWindowLength) + 1.0) / fs;
        int baseTimeLength = (halfWindowLength * 2) + 1;
        int fftSize = (int)Math.Pow(
            2.0, 2.0 + (int)(Math.Log((halfWindowLength * 2.0) + 1.0) / WorldConstants.Log2));

        using WorldArenaScope scope = arena.BeginScope();
        StoneMaskScratch scratch = StoneMaskScratch.Bind(arena, baseTimeLength, fftSize);

        for (int i = 0; i < baseTimeLength; i++)
        {
            scratch.BaseTime[i] = (double)(-halfWindowLength + i) / fs;
        }

        double meanF0 = GetMeanF0(x, xLength, fs, currentPosition, initialF0, fftSize,
            windowLengthInTime, baseTimeLength, scratch);

        if (Math.Abs(meanF0 - initialF0) > initialF0 * 0.2)
        {
            meanF0 = initialF0;
        }

        return meanF0;
    }

    private static double GetMeanF0(double* x, int xLength, int fs, double currentPosition,
        double initialF0, int fftSize, double windowLengthInTime, int baseTimeLength,
        in StoneMaskScratch scratch)
    {
        GetBaseIndex(currentPosition, scratch.BaseTime, baseTimeLength, fs, scratch.IndexRaw);
        GetMainWindow(currentPosition, scratch.IndexRaw, baseTimeLength, fs, windowLengthInTime,
            scratch.MainWindow);
        GetDiffWindow(scratch.MainWindow, baseTimeLength, scratch.DiffWindow);
        GetSpectra(x, xLength, fftSize, baseTimeLength, scratch);

        for (int j = 0; j <= fftSize / 2; ++j)
        {
            scratch.NumeratorI[j] =
                (scratch.MainSpectrum[j].Real * scratch.DiffSpectrum[j].Imaginary)
                - (scratch.MainSpectrum[j].Imaginary * scratch.DiffSpectrum[j].Real);
            scratch.PowerSpectrum[j] =
                (scratch.MainSpectrum[j].Real * scratch.MainSpectrum[j].Real)
                + (scratch.MainSpectrum[j].Imaginary * scratch.MainSpectrum[j].Imaginary);
        }

        return GetTentativeF0(scratch.PowerSpectrum, scratch.NumeratorI, fftSize, fs, initialF0,
            scratch);
    }

    private static void GetBaseIndex(double currentPosition, double* baseTime,
        int baseTimeLength, int fs, int* indexRaw)
    {
        for (int i = 0; i < baseTimeLength; ++i)
        {
            indexRaw[i] = MatlabFunctions.MatlabRound((currentPosition + baseTime[i]) * fs);
        }
    }

    private static void GetMainWindow(double currentPosition, int* indexRaw, int baseTimeLength,
        int fs, double windowLengthInTime, double* mainWindow)
    {
        for (int i = 0; i < baseTimeLength; ++i)
        {
            double tmp = ((indexRaw[i] - 1.0) / fs) - currentPosition;
            mainWindow[i] = 0.42
                + (0.5 * Math.Cos(2.0 * WorldConstants.Pi * tmp / windowLengthInTime))
                + (0.08 * Math.Cos(4.0 * WorldConstants.Pi * tmp / windowLengthInTime));
        }
    }

    private static void GetDiffWindow(double* mainWindow, int baseTimeLength, double* diffWindow)
    {
        diffWindow[0] = -mainWindow[1] / 2.0;
        for (int i = 1; i < baseTimeLength - 1; ++i)
        {
            diffWindow[i] = -(mainWindow[i + 1] - mainWindow[i - 1]) / 2.0;
        }
        diffWindow[baseTimeLength - 1] = mainWindow[baseTimeLength - 2] / 2.0;
    }

    private static void GetSpectra(double* x, int xLength, int fftSize, int baseTimeLength,
        in StoneMaskScratch scratch)
    {
        int* index = scratch.Index;

        for (int i = 0; i < baseTimeLength; ++i)
        {
            index[i] = WorldMath.MaxInt(0, WorldMath.MinInt(xLength - 1, scratch.IndexRaw[i] - 1));
        }
        for (int i = 0; i < baseTimeLength; ++i)
        {
            scratch.ForwardRealFft.Waveform[i] = x[index[i]] * scratch.MainWindow[i];
        }
        for (int i = baseTimeLength; i < fftSize; ++i)
        {
            scratch.ForwardRealFft.Waveform[i] = 0.0;
        }

        scratch.ForwardRealFft.ForwardFft.Execute();
        for (int i = 0; i <= fftSize / 2; ++i)
        {
            scratch.MainSpectrum[i] = scratch.ForwardRealFft.Spectrum[i];
        }

        for (int i = 0; i < baseTimeLength; ++i)
        {
            scratch.ForwardRealFft.Waveform[i] = x[index[i]] * scratch.DiffWindow[i];
        }
        for (int i = baseTimeLength; i < fftSize; ++i)
        {
            scratch.ForwardRealFft.Waveform[i] = 0.0;
        }
        scratch.ForwardRealFft.ForwardFft.Execute();
        for (int i = 0; i <= fftSize / 2; ++i)
        {
            scratch.DiffSpectrum[i] = scratch.ForwardRealFft.Spectrum[i];
        }
    }

    private static double GetTentativeF0(double* powerSpectrum, double* numeratorI, int fftSize,
        int fs, double initialF0, in StoneMaskScratch scratch)
    {
        double tentativeF0 = FixF0(powerSpectrum, numeratorI, fftSize, fs, initialF0, 2, scratch);

        if (tentativeF0 <= 0.0 || tentativeF0 > initialF0 * 2)
        {
            return 0.0;
        }

        return FixF0(powerSpectrum, numeratorI, fftSize, fs, tentativeF0, 6, scratch);
    }

    private static double FixF0(double* powerSpectrum, double* numeratorI, int fftSize, int fs,
        double initialF0, int numberOfHarmonics, in StoneMaskScratch scratch)
    {
        double* amplitudeList = scratch.AmplitudeList;
        double* instantaneousFrequencyList = scratch.InstantaneousFrequencyList;

        for (int i = 0; i < numberOfHarmonics; ++i)
        {
            int index = WorldMath.MinInt(
                MatlabFunctions.MatlabRound(initialF0 * fftSize / fs * (i + 1)), fftSize / 2);
            instantaneousFrequencyList[i] = powerSpectrum[index] == 0.0
                ? 0.0
                : ((double)index * fs / fftSize)
                    + (numeratorI[index] / powerSpectrum[index] * fs / 2.0 / WorldConstants.Pi);
            amplitudeList[i] = Math.Sqrt(powerSpectrum[index]);
        }

        double denominator = 0.0;
        double numerator = 0.0;
        for (int i = 0; i < numberOfHarmonics; ++i)
        {
            numerator += amplitudeList[i] * instantaneousFrequencyList[i];
            denominator += amplitudeList[i] * (i + 1);
        }

        return numerator / (denominator + WorldConstants.MySafeGuardMinimum);
    }
}
