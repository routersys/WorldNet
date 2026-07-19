namespace WorldNet;

public static unsafe class CheapTrick
{
    public static int GetFftSize(int fs, double f0Floor)
    {
        return (int)Math.Pow(2.0,
            1.0 + (int)(Math.Log((3.0 * fs / f0Floor) + 1) / WorldConstants.Log2));
    }

    public static double GetF0Floor(int fs, int fftSize)
    {
        return 3.0 * fs / (fftSize - 3.0);
    }

    public static void Estimate(ReadOnlySpan<double> x, int fs, CheapTrickOption option,
        ReadOnlySpan<double> temporalPositions, ReadOnlySpan<double> f0, Span<double> spectrogram,
        WorldArena arena)
    {
        ArgumentNullException.ThrowIfNull(arena);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fs);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(option.FftSize);

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

        int fftSize = option.FftSize;
        int spectrumLength = (fftSize / 2) + 1;
        int f0Length = f0.Length;

        if (spectrogram.Length < (long)f0Length * spectrumLength)
        {
            throw new ArgumentException(
                "The spectrogram destination is smaller than f0 length times the spectrum length.",
                nameof(spectrogram));
        }

        if (f0Length == 0)
        {
            return;
        }

        using WorldArenaScope scope = arena.BeginScope();
        CheapTrickScratch scratch = CheapTrickScratch.Bind(arena, fftSize);

        RandnState randnState = default;
        randnState.Reseed();

        double f0Floor = GetF0Floor(fs, fftSize);

        fixed (double* xPointer = x)
        fixed (double* positionPointer = temporalPositions)
        fixed (double* f0Pointer = f0)
        fixed (double* spectrogramPointer = spectrogram)
        {
            for (int i = 0; i < f0Length; ++i)
            {
                double currentF0 =
                    f0Pointer[i] <= f0Floor ? WorldConstants.DefaultF0 : f0Pointer[i];
                int halfWindowLength = MatlabFunctions.MatlabRound(1.5 * fs / currentF0);
                int upperLimit = Common.GetDcCorrectionUpperLimit(currentF0, fs, fftSize);
                int boundary =
                    Common.GetLinearSmoothingBoundary(currentF0 * 2.0 / 3.0, fs, fftSize);

                using WorldArenaScope frameScope = arena.BeginScope();
                CheapTrickFrameScratch frame =
                    CheapTrickFrameScratch.Bind(arena, halfWindowLength, upperLimit, fftSize,
                        boundary);

                CheapTrickGeneralBody(xPointer, x.Length, fs, currentF0, fftSize,
                    positionPointer[i], option.Q1, scratch, frame, ref randnState);

                double* row = spectrogramPointer + ((long)i * spectrumLength);
                for (int j = 0; j <= fftSize / 2; ++j)
                {
                    row[j] = scratch.SpectralEnvelope[j];
                }
            }
        }
    }

    private static void SmoothingWithRecovery(double f0, int fs, int fftSize, double q1,
        in CheapTrickScratch scratch)
    {
        double* smoothingLifter = scratch.SmoothingLifter;
        double* compensationLifter = scratch.CompensationLifter;

        smoothingLifter[0] = 1.0;
        compensationLifter[0] = (1.0 - (2.0 * q1)) + (2.0 * q1);
        for (int i = 1; i <= scratch.ForwardRealFft.FftSize / 2; ++i)
        {
            double quefrency = (double)i / fs;
            smoothingLifter[i] = Math.Sin(WorldConstants.Pi * f0 * quefrency) /
                (WorldConstants.Pi * f0 * quefrency);
            compensationLifter[i] = (1.0 - (2.0 * q1)) +
                (2.0 * q1 * Math.Cos(2.0 * WorldConstants.Pi * quefrency * f0));
        }

        double* waveform = scratch.ForwardRealFft.Waveform;
        for (int i = 0; i <= fftSize / 2; ++i)
        {
            waveform[i] = Math.Log(waveform[i]);
        }
        for (int i = 1; i < fftSize / 2; ++i)
        {
            waveform[fftSize - i] = waveform[i];
        }
        scratch.ForwardRealFft.ForwardFft.Execute();

        FftComplex* forwardSpectrum = scratch.ForwardRealFft.Spectrum;
        FftComplex* inverseSpectrum = scratch.InverseRealFft.Spectrum;
        for (int i = 0; i <= fftSize / 2; ++i)
        {
            inverseSpectrum[i].Real = forwardSpectrum[i].Real *
                smoothingLifter[i] * compensationLifter[i] / fftSize;
            inverseSpectrum[i].Imaginary = 0.0;
        }
        scratch.InverseRealFft.InverseFft.Execute();

        double* inverseWaveform = scratch.InverseRealFft.Waveform;
        double* spectralEnvelope = scratch.SpectralEnvelope;
        for (int i = 0; i <= fftSize / 2; ++i)
        {
            spectralEnvelope[i] = Math.Exp(inverseWaveform[i]);
        }
    }

    private static void GetPowerSpectrum(int fs, double f0, int fftSize,
        in CheapTrickScratch scratch, in CheapTrickFrameScratch frame)
    {
        int halfWindowLength = MatlabFunctions.MatlabRound(1.5 * fs / f0);

        double* waveform = scratch.ForwardRealFft.Waveform;
        for (int i = (halfWindowLength * 2) + 1; i < fftSize; ++i)
        {
            waveform[i] = 0.0;
        }
        scratch.ForwardRealFft.ForwardFft.Execute();

        double* powerSpectrum = waveform;
        FftComplex* spectrum = scratch.ForwardRealFft.Spectrum;
        for (int i = 0; i <= fftSize / 2; ++i)
        {
            powerSpectrum[i] = (spectrum[i].Real * spectrum[i].Real) +
                (spectrum[i].Imaginary * spectrum[i].Imaginary);
        }

        Common.DcCorrection(powerSpectrum, f0, fs, fftSize, powerSpectrum, frame.DcCorrection);
    }

    private static void SetParametersForGetWindowedWaveform(int halfWindowLength, int xLength,
        double currentPosition, int fs, double currentF0, int* baseIndex, int* safeIndex,
        double* window)
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

        double average = 0.0;
        for (int i = 0; i <= halfWindowLength * 2; ++i)
        {
            double position = baseIndex[i] / 1.5 / fs;
            window[i] = (0.5 * Math.Cos(WorldConstants.Pi * position * currentF0)) + 0.5;
            average += window[i] * window[i];
        }
        average = Math.Sqrt(average);
        for (int i = 0; i <= halfWindowLength * 2; ++i)
        {
            window[i] /= average;
        }
    }

    private static void GetWindowedWaveform(double* x, int xLength, int fs, double currentF0,
        double currentPosition, in CheapTrickScratch scratch, in CheapTrickFrameScratch frame,
        ref RandnState randnState)
    {
        int halfWindowLength = MatlabFunctions.MatlabRound(1.5 * fs / currentF0);

        int* baseIndex = frame.BaseIndex;
        int* safeIndex = frame.SafeIndex;
        double* window = frame.Window;

        SetParametersForGetWindowedWaveform(halfWindowLength, xLength, currentPosition, fs,
            currentF0, baseIndex, safeIndex, window);

        double* waveform = scratch.ForwardRealFft.Waveform;
        for (int i = 0; i <= halfWindowLength * 2; ++i)
        {
            waveform[i] = (x[safeIndex[i]] * window[i]) +
                (randnState.Next() * WorldConstants.MySafeGuardMinimum);
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

    private static void AddInfinitesimalNoise(double* inputSpectrum, int fftSize,
        double* outputSpectrum, ref RandnState randnState)
    {
        for (int i = 0; i <= fftSize / 2; ++i)
        {
            outputSpectrum[i] =
                inputSpectrum[i] + (Math.Abs(randnState.Next()) * WorldConstants.Eps);
        }
    }

    private static void CheapTrickGeneralBody(double* x, int xLength, int fs, double currentF0,
        int fftSize, double currentPosition, double q1, in CheapTrickScratch scratch,
        in CheapTrickFrameScratch frame, ref RandnState randnState)
    {
        GetWindowedWaveform(x, xLength, fs, currentF0, currentPosition, scratch, frame,
            ref randnState);

        GetPowerSpectrum(fs, currentF0, fftSize, scratch, frame);

        Common.LinearSmoothing(scratch.ForwardRealFft.Waveform, currentF0 * 2.0 / 3.0, fs, fftSize,
            scratch.ForwardRealFft.Waveform, frame.LinearSmoothing);

        AddInfinitesimalNoise(scratch.ForwardRealFft.Waveform, fftSize,
            scratch.ForwardRealFft.Waveform, ref randnState);

        SmoothingWithRecovery(currentF0, fs, fftSize, q1, scratch);
    }
}
