namespace WorldNet;

public static unsafe class D4C
{
    public static void Estimate(ReadOnlySpan<double> x, int fs, D4COption option,
        ReadOnlySpan<double> temporalPositions, ReadOnlySpan<double> f0, int fftSize,
        Span<double> aperiodicity, WorldArena arena)
    {
        ArgumentNullException.ThrowIfNull(arena);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fs);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fftSize);

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

        int spectrumLength = (fftSize / 2) + 1;
        int f0Length = f0.Length;

        if (aperiodicity.Length < (long)f0Length * spectrumLength)
        {
            throw new ArgumentException(
                "The aperiodicity destination is smaller than f0 length times the spectrum length.",
                nameof(aperiodicity));
        }

        if (f0Length == 0)
        {
            return;
        }

        using WorldArenaScope scope = arena.BeginScope();

        RandnState randnState = default;
        randnState.Reseed();

        fixed (double* xPointer = x)
        fixed (double* positionPointer = temporalPositions)
        fixed (double* f0Pointer = f0)
        fixed (double* aperiodicityPointer = aperiodicity)
        {
            InitializeAperiodicity(f0Length, fftSize, spectrumLength, aperiodicityPointer);

            int fftSizeD4C = (int)Math.Pow(2.0, 1.0 +
                (int)(Math.Log((4.0 * fs / WorldConstants.FloorF0D4C) + 1) / WorldConstants.Log2));

            ForwardRealFft forwardRealFft = ForwardRealFft.Bind(arena, fftSizeD4C);

            int numberOfAperiodicities = (int)(WorldMath.MinDouble(WorldConstants.UpperLimit,
                (fs / 2.0) - WorldConstants.FrequencyInterval) / WorldConstants.FrequencyInterval);

            int windowLength =
                ((int)(WorldConstants.FrequencyInterval * fftSizeD4C / fs) * 2) + 1;
            double* window = (double*)arena.AllocateRaw(windowLength, sizeof(double));
            Common.NuttallWindow(windowLength, window);

            double* aperiodicity0 = (double*)arena.AllocateRaw(f0Length, sizeof(double));
            D4CLoveTrain(xPointer, fs, x.Length, f0Pointer, f0Length, positionPointer,
                aperiodicity0, arena, ref randnState);

            double* coarseAperiodicity =
                (double*)arena.AllocateRaw(numberOfAperiodicities + 2, sizeof(double));
            coarseAperiodicity[0] = -60.0;
            coarseAperiodicity[numberOfAperiodicities + 1] = -WorldConstants.MySafeGuardMinimum;
            double* coarseFrequencyAxis =
                (double*)arena.AllocateRaw(numberOfAperiodicities + 2, sizeof(double));
            for (int i = 0; i <= numberOfAperiodicities; ++i)
            {
                coarseFrequencyAxis[i] = i * WorldConstants.FrequencyInterval;
            }
            coarseFrequencyAxis[numberOfAperiodicities + 1] = fs / 2.0;

            double* frequencyAxis = (double*)arena.AllocateRaw(spectrumLength, sizeof(double));
            for (int i = 0; i <= fftSize / 2; ++i)
            {
                frequencyAxis[i] = (double)i * fs / fftSize;
            }

            for (int i = 0; i < f0Length; ++i)
            {
                if (f0Pointer[i] == 0 || aperiodicity0[i] <= option.Threshold)
                {
                    continue;
                }
                D4CGeneralBody(xPointer, x.Length, fs,
                    WorldMath.MaxDouble(WorldConstants.FloorF0D4C, f0Pointer[i]), fftSizeD4C,
                    positionPointer[i], numberOfAperiodicities, window, windowLength,
                    forwardRealFft, coarseAperiodicity + 1, arena, ref randnState);

                GetAperiodicity(coarseFrequencyAxis, coarseAperiodicity, numberOfAperiodicities,
                    frequencyAxis, fftSize, aperiodicityPointer + ((long)i * spectrumLength),
                    arena);
            }
        }
    }

    private static void DcCorrection(double* input, double f0, int fs, int fftSize,
        double* output, WorldArena arena)
    {
        using WorldArenaScope scope = arena.BeginScope();
        int upperLimit = Common.GetDcCorrectionUpperLimit(f0, fs, fftSize);
        DcCorrectionScratch scratch = DcCorrectionScratch.Bind(arena, upperLimit);
        Common.DcCorrection(input, f0, fs, fftSize, output, scratch);
    }

    private static void LinearSmoothing(double* input, double width, int fs, int fftSize,
        double* output, WorldArena arena)
    {
        using WorldArenaScope scope = arena.BeginScope();
        int boundary = Common.GetLinearSmoothingBoundary(width, fs, fftSize);
        LinearSmoothingScratch scratch = LinearSmoothingScratch.Bind(arena, fftSize, boundary);
        Common.LinearSmoothing(input, width, fs, fftSize, output, scratch);
    }

    private static void Interp1(double* x, double* y, int xLength, double* xi, int xiLength,
        double* yi, WorldArena arena)
    {
        using WorldArenaScope scope = arena.BeginScope();
        Interp1Scratch scratch = Interp1Scratch.Bind(arena, xLength, xiLength);
        MatlabFunctions.Interp1(x, y, xLength, xi, xiLength, yi, scratch);
    }

    private static void SetParametersForGetWindowedWaveform(int halfWindowLength, int xLength,
        double currentPosition, int fs, double currentF0, int windowType,
        double windowLengthRatio, int* baseIndex, int* safeIndex, double* window)
    {
        for (int i = -halfWindowLength; i <= halfWindowLength; ++i)
        {
            baseIndex[i + halfWindowLength] = i;
        }
        int origin = MatlabFunctions.MatlabRound((currentPosition * fs) + 0.001);
        for (int i = 0; i <= halfWindowLength * 2; ++i)
        {
            safeIndex[i] =
                WorldMath.MinInt(xLength - 1, WorldMath.MaxInt(0, origin + baseIndex[i]));
        }

        if (windowType == WorldConstants.Hanning)
        {
            for (int i = 0; i <= halfWindowLength * 2; ++i)
            {
                double position = (2.0 * baseIndex[i] / windowLengthRatio) / fs;
                window[i] = (0.5 * Math.Cos(WorldConstants.Pi * position * currentF0)) + 0.5;
            }
        }
        else
        {
            for (int i = 0; i <= halfWindowLength * 2; ++i)
            {
                double position = (2.0 * baseIndex[i] / windowLengthRatio) / fs;
                window[i] = 0.42 +
                    (0.5 * Math.Cos(WorldConstants.Pi * position * currentF0)) +
                    (0.08 * Math.Cos(WorldConstants.Pi * position * currentF0 * 2));
            }
        }
    }

    private static void GetWindowedWaveform(double* x, int xLength, int fs, double currentF0,
        double currentPosition, int windowType, double windowLengthRatio, double* waveform,
        WorldArena arena, ref RandnState randnState)
    {
        int halfWindowLength =
            MatlabFunctions.MatlabRound(windowLengthRatio * fs / currentF0 / 2.0);

        using WorldArenaScope scope = arena.BeginScope();
        int windowLength = (halfWindowLength * 2) + 1;
        int* baseIndex = (int*)arena.AllocateRaw(windowLength, sizeof(int));
        int* safeIndex = (int*)arena.AllocateRaw(windowLength, sizeof(int));
        double* window = (double*)arena.AllocateRaw(windowLength, sizeof(double));

        SetParametersForGetWindowedWaveform(halfWindowLength, xLength, currentPosition, fs,
            currentF0, windowType, windowLengthRatio, baseIndex, safeIndex, window);

        for (int i = 0; i <= halfWindowLength * 2; ++i)
        {
            waveform[i] = (x[safeIndex[i]] * window[i]) +
                (randnState.Next() * WorldConstants.SafeGuardD4C);
        }

        double tmpWeight1 = 0;
        double tmpWeight2 = 0;
        for (int i = 0; i <= halfWindowLength * 2; ++i)
        {
            tmpWeight1 += waveform[i];
            tmpWeight2 += window[i];
        }
        double weightingCoefficient = tmpWeight1 / tmpWeight2;
        for (int i = 0; i <= halfWindowLength * 2; ++i)
        {
            waveform[i] -= window[i] * weightingCoefficient;
        }
    }

    private static void GetCentroid(double* x, int xLength, int fs, double currentF0,
        int fftSize, double currentPosition, in ForwardRealFft forwardRealFft, double* centroid,
        WorldArena arena, ref RandnState randnState)
    {
        for (int i = 0; i < fftSize; ++i)
        {
            forwardRealFft.Waveform[i] = 0.0;
        }
        GetWindowedWaveform(x, xLength, fs, currentF0, currentPosition, WorldConstants.Blackman,
            4.0, forwardRealFft.Waveform, arena, ref randnState);

        double power = 0.0;
        for (int i = 0; i <= MatlabFunctions.MatlabRound(2.0 * fs / currentF0) * 2; ++i)
        {
            power += forwardRealFft.Waveform[i] * forwardRealFft.Waveform[i];
        }
        for (int i = 0; i <= MatlabFunctions.MatlabRound(2.0 * fs / currentF0) * 2; ++i)
        {
            forwardRealFft.Waveform[i] /= Math.Sqrt(power);
        }

        forwardRealFft.ForwardFft.Execute();

        using WorldArenaScope scope = arena.BeginScope();
        double* tmpReal = (double*)arena.AllocateRaw((fftSize / 2) + 1, sizeof(double));
        double* tmpImag = (double*)arena.AllocateRaw((fftSize / 2) + 1, sizeof(double));
        for (int i = 0; i <= fftSize / 2; ++i)
        {
            tmpReal[i] = forwardRealFft.Spectrum[i].Real;
            tmpImag[i] = forwardRealFft.Spectrum[i].Imaginary;
        }

        for (int i = 0; i < fftSize; ++i)
        {
            forwardRealFft.Waveform[i] *= i + 1.0;
        }
        forwardRealFft.ForwardFft.Execute();
        for (int i = 0; i <= fftSize / 2; ++i)
        {
            centroid[i] = (forwardRealFft.Spectrum[i].Real * tmpReal[i]) +
                (tmpImag[i] * forwardRealFft.Spectrum[i].Imaginary);
        }
    }

    private static void GetStaticCentroid(double* x, int xLength, int fs, double currentF0,
        int fftSize, double currentPosition, in ForwardRealFft forwardRealFft,
        double* staticCentroid, WorldArena arena, ref RandnState randnState)
    {
        using WorldArenaScope scope = arena.BeginScope();
        double* centroid1 = (double*)arena.AllocateRaw((fftSize / 2) + 1, sizeof(double));
        double* centroid2 = (double*)arena.AllocateRaw((fftSize / 2) + 1, sizeof(double));

        GetCentroid(x, xLength, fs, currentF0, fftSize, currentPosition - (0.25 / currentF0),
            forwardRealFft, centroid1, arena, ref randnState);
        GetCentroid(x, xLength, fs, currentF0, fftSize, currentPosition + (0.25 / currentF0),
            forwardRealFft, centroid2, arena, ref randnState);

        for (int i = 0; i <= fftSize / 2; ++i)
        {
            staticCentroid[i] = centroid1[i] + centroid2[i];
        }

        DcCorrection(staticCentroid, currentF0, fs, fftSize, staticCentroid, arena);
    }

    private static void GetSmoothedPowerSpectrum(double* x, int xLength, int fs,
        double currentF0, int fftSize, double currentPosition, in ForwardRealFft forwardRealFft,
        double* smoothedPowerSpectrum, WorldArena arena, ref RandnState randnState)
    {
        for (int i = 0; i < fftSize; ++i)
        {
            forwardRealFft.Waveform[i] = 0.0;
        }
        GetWindowedWaveform(x, xLength, fs, currentF0, currentPosition, WorldConstants.Hanning,
            4.0, forwardRealFft.Waveform, arena, ref randnState);

        forwardRealFft.ForwardFft.Execute();
        for (int i = 0; i <= fftSize / 2; ++i)
        {
            smoothedPowerSpectrum[i] =
                (forwardRealFft.Spectrum[i].Real * forwardRealFft.Spectrum[i].Real) +
                (forwardRealFft.Spectrum[i].Imaginary * forwardRealFft.Spectrum[i].Imaginary);
        }
        DcCorrection(smoothedPowerSpectrum, currentF0, fs, fftSize, smoothedPowerSpectrum, arena);
        LinearSmoothing(smoothedPowerSpectrum, currentF0, fs, fftSize, smoothedPowerSpectrum,
            arena);
    }

    private static void GetStaticGroupDelay(double* staticCentroid,
        double* smoothedPowerSpectrum, int fs, double f0, int fftSize, double* staticGroupDelay,
        WorldArena arena)
    {
        for (int i = 0; i <= fftSize / 2; ++i)
        {
            staticGroupDelay[i] = staticCentroid[i] / smoothedPowerSpectrum[i];
        }
        LinearSmoothing(staticGroupDelay, f0 / 2.0, fs, fftSize, staticGroupDelay, arena);

        using WorldArenaScope scope = arena.BeginScope();
        double* smoothedGroupDelay = (double*)arena.AllocateRaw((fftSize / 2) + 1, sizeof(double));
        LinearSmoothing(staticGroupDelay, f0, fs, fftSize, smoothedGroupDelay, arena);

        for (int i = 0; i <= fftSize / 2; ++i)
        {
            staticGroupDelay[i] -= smoothedGroupDelay[i];
        }
    }

    private static void GetCoarseAperiodicity(double* staticGroupDelay, int fs, int fftSize,
        int numberOfAperiodicities, double* window, int windowLength,
        in ForwardRealFft forwardRealFft, double* coarseAperiodicity, WorldArena arena)
    {
        int boundary = MatlabFunctions.MatlabRound(fftSize * 8.0 / windowLength);
        int halfWindowLength = windowLength / 2;

        for (int i = 0; i < fftSize; ++i)
        {
            forwardRealFft.Waveform[i] = 0.0;
        }

        using WorldArenaScope scope = arena.BeginScope();
        double* powerSpectrum = (double*)arena.AllocateRaw((fftSize / 2) + 1, sizeof(double));
        for (int i = 0; i < numberOfAperiodicities; ++i)
        {
            int center = (int)(WorldConstants.FrequencyInterval * (i + 1) * fftSize / fs);
            for (int j = 0; j <= halfWindowLength * 2; ++j)
            {
                forwardRealFft.Waveform[j] =
                    staticGroupDelay[center - halfWindowLength + j] * window[j];
            }
            forwardRealFft.ForwardFft.Execute();
            for (int j = 0; j <= fftSize / 2; ++j)
            {
                powerSpectrum[j] =
                    (forwardRealFft.Spectrum[j].Real * forwardRealFft.Spectrum[j].Real) +
                    (forwardRealFft.Spectrum[j].Imaginary * forwardRealFft.Spectrum[j].Imaginary);
            }
            new Span<double>(powerSpectrum, (fftSize / 2) + 1).Sort();
            for (int j = 1; j <= fftSize / 2; ++j)
            {
                powerSpectrum[j] += powerSpectrum[j - 1];
            }
            coarseAperiodicity[i] = 10 * Math.Log10(
                powerSpectrum[(fftSize / 2) - boundary - 1] / powerSpectrum[fftSize / 2]);
        }
    }

    private static double D4CLoveTrainSub(double* x, int fs, int xLength, double currentF0,
        double currentPosition, int fftSize, int boundary0, int boundary1, int boundary2,
        in ForwardRealFft forwardRealFft, WorldArena arena, ref RandnState randnState)
    {
        using WorldArenaScope scope = arena.BeginScope();
        double* powerSpectrum = (double*)arena.AllocateRaw(fftSize, sizeof(double));

        int windowLength = (MatlabFunctions.MatlabRound(1.5 * fs / currentF0) * 2) + 1;
        GetWindowedWaveform(x, xLength, fs, currentF0, currentPosition, WorldConstants.Blackman,
            3.0, forwardRealFft.Waveform, arena, ref randnState);

        for (int i = windowLength; i < fftSize; ++i)
        {
            forwardRealFft.Waveform[i] = 0.0;
        }
        forwardRealFft.ForwardFft.Execute();

        for (int i = 0; i <= boundary0; ++i)
        {
            powerSpectrum[i] = 0.0;
        }
        for (int i = boundary0 + 1; i < (fftSize / 2) + 1; ++i)
        {
            powerSpectrum[i] =
                (forwardRealFft.Spectrum[i].Real * forwardRealFft.Spectrum[i].Real) +
                (forwardRealFft.Spectrum[i].Imaginary * forwardRealFft.Spectrum[i].Imaginary);
        }
        for (int i = boundary0; i <= boundary2; ++i)
        {
            powerSpectrum[i] += powerSpectrum[i - 1];
        }

        return powerSpectrum[boundary1] / powerSpectrum[boundary2];
    }

    private static void D4CLoveTrain(double* x, int fs, int xLength, double* f0, int f0Length,
        double* temporalPositions, double* aperiodicity0, WorldArena arena,
        ref RandnState randnState)
    {
        double lowestF0 = 40.0;
        int fftSize = (int)Math.Pow(2.0, 1.0 +
            (int)(Math.Log((3.0 * fs / lowestF0) + 1) / WorldConstants.Log2));

        using WorldArenaScope scope = arena.BeginScope();
        ForwardRealFft forwardRealFft = ForwardRealFft.Bind(arena, fftSize);

        int boundary0 = (int)Math.Ceiling(100.0 * fftSize / fs);
        int boundary1 = (int)Math.Ceiling(4000.0 * fftSize / fs);
        int boundary2 = (int)Math.Ceiling(7900.0 * fftSize / fs);
        for (int i = 0; i < f0Length; ++i)
        {
            if (f0[i] == 0.0)
            {
                aperiodicity0[i] = 0.0;
                continue;
            }
            aperiodicity0[i] = D4CLoveTrainSub(x, fs, xLength,
                WorldMath.MaxDouble(f0[i], lowestF0), temporalPositions[i], fftSize, boundary0,
                boundary1, boundary2, forwardRealFft, arena, ref randnState);
        }
    }

    private static void D4CGeneralBody(double* x, int xLength, int fs, double currentF0,
        int fftSize, double currentPosition, int numberOfAperiodicities, double* window,
        int windowLength, in ForwardRealFft forwardRealFft, double* coarseAperiodicity,
        WorldArena arena, ref RandnState randnState)
    {
        using WorldArenaScope scope = arena.BeginScope();
        double* staticCentroid = (double*)arena.AllocateRaw((fftSize / 2) + 1, sizeof(double));
        double* smoothedPowerSpectrum =
            (double*)arena.AllocateRaw((fftSize / 2) + 1, sizeof(double));
        double* staticGroupDelay = (double*)arena.AllocateRaw((fftSize / 2) + 1, sizeof(double));

        GetStaticCentroid(x, xLength, fs, currentF0, fftSize, currentPosition, forwardRealFft,
            staticCentroid, arena, ref randnState);
        GetSmoothedPowerSpectrum(x, xLength, fs, currentF0, fftSize, currentPosition,
            forwardRealFft, smoothedPowerSpectrum, arena, ref randnState);
        GetStaticGroupDelay(staticCentroid, smoothedPowerSpectrum, fs, currentF0, fftSize,
            staticGroupDelay, arena);

        GetCoarseAperiodicity(staticGroupDelay, fs, fftSize, numberOfAperiodicities, window,
            windowLength, forwardRealFft, coarseAperiodicity, arena);

        for (int i = 0; i < numberOfAperiodicities; ++i)
        {
            coarseAperiodicity[i] = WorldMath.MinDouble(0.0,
                coarseAperiodicity[i] + ((currentF0 - 100) / 50.0));
        }
    }

    private static void InitializeAperiodicity(int f0Length, int fftSize, int spectrumLength,
        double* aperiodicity)
    {
        for (int i = 0; i < f0Length; ++i)
        {
            for (int j = 0; j < (fftSize / 2) + 1; ++j)
            {
                aperiodicity[((long)i * spectrumLength) + j] =
                    1.0 - WorldConstants.MySafeGuardMinimum;
            }
        }
    }

    private static void GetAperiodicity(double* coarseFrequencyAxis, double* coarseAperiodicity,
        int numberOfAperiodicities, double* frequencyAxis, int fftSize, double* aperiodicity,
        WorldArena arena)
    {
        Interp1(coarseFrequencyAxis, coarseAperiodicity, numberOfAperiodicities + 2,
            frequencyAxis, (fftSize / 2) + 1, aperiodicity, arena);
        for (int i = 0; i <= fftSize / 2; ++i)
        {
            aperiodicity[i] = Math.Pow(10.0, aperiodicity[i] / 20.0);
        }
    }
}
