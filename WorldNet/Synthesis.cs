namespace WorldNet;

public static unsafe class Synthesis
{
    public static void Synthesize(ReadOnlySpan<double> f0, ReadOnlySpan<double> spectrogram,
        ReadOnlySpan<double> aperiodicity, int fftSize, double framePeriod, int fs,
        Span<double> y, WorldArena arena)
    {
        ArgumentNullException.ThrowIfNull(arena);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fs);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fftSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(framePeriod);

        if (f0.Length < 2)
        {
            throw new ArgumentException("The F0 contour must have at least two frames.",
                nameof(f0));
        }

        if (y.IsEmpty)
        {
            throw new ArgumentException("The destination must not be empty.", nameof(y));
        }

        int f0Length = f0.Length;
        int spectrumLength = (fftSize / 2) + 1;

        if (spectrogram.Length < (long)f0Length * spectrumLength)
        {
            throw new ArgumentException(
                "The spectrogram is smaller than f0 length times the spectrum length.",
                nameof(spectrogram));
        }

        if (aperiodicity.Length < (long)f0Length * spectrumLength)
        {
            throw new ArgumentException(
                "The aperiodicity is smaller than f0 length times the spectrum length.",
                nameof(aperiodicity));
        }

        int yLength = y.Length;

        using WorldArenaScope scope = arena.BeginScope();
        SynthesisScratch scratch = SynthesisScratch.Bind(arena, fftSize, yLength, f0Length);

        RandnState randnState = default;
        randnState.Reseed();

        fixed (double* f0Pointer = f0)
        fixed (double* spectrogramPointer = spectrogram)
        fixed (double* aperiodicityPointer = aperiodicity)
        fixed (double* yPointer = y)
        {
            for (int i = 0; i < yLength; ++i)
            {
                yPointer[i] = 0.0;
            }

            int numberOfPulses = GetTimeBase(f0Pointer, f0Length, fs, framePeriod / 1000.0,
                yLength, (fs / fftSize) + 1.0, scratch);

            GetDCRemover(fftSize, scratch.DcRemover);

            framePeriod /= 1000.0;
            for (int i = 0; i < numberOfPulses; ++i)
            {
                int noiseSize =
                    scratch.PulseLocationsIndex[WorldMath.MinInt(numberOfPulses - 1, i + 1)] -
                    scratch.PulseLocationsIndex[i];

                GetOneFrameSegment(scratch.InterpolatedVuv[scratch.PulseLocationsIndex[i]],
                    noiseSize, spectrogramPointer, fftSize, aperiodicityPointer, spectrumLength,
                    f0Length, framePeriod, scratch.PulseLocations[i],
                    scratch.PulseLocationsTimeShift[i], fs, scratch, ref randnState);

                int offset = scratch.PulseLocationsIndex[i] - (fftSize / 2) + 1;
                int lowerLimit = WorldMath.MaxInt(0, -offset);
                int upperLimit = WorldMath.MinInt(fftSize, yLength - offset);
                for (int j = lowerLimit; j < upperLimit; ++j)
                {
                    int index = j + offset;
                    yPointer[index] += scratch.ImpulseResponse[j];
                }
            }
        }
    }

    private static void GetNoiseSpectrum(int noiseSize, int fftSize,
        in ForwardRealFft forwardRealFft, ref RandnState randnState)
    {
        double average = 0.0;
        for (int i = 0; i < noiseSize; ++i)
        {
            forwardRealFft.Waveform[i] = randnState.Next();
            average += forwardRealFft.Waveform[i];
        }

        average /= noiseSize;
        for (int i = 0; i < noiseSize; ++i)
        {
            forwardRealFft.Waveform[i] -= average;
        }
        for (int i = noiseSize; i < fftSize; ++i)
        {
            forwardRealFft.Waveform[i] = 0.0;
        }
        forwardRealFft.ForwardFft.Execute();
    }

    private static void GetAperiodicResponse(int noiseSize, int fftSize, double* spectrum,
        double* aperiodicRatio, double currentVuv, in SynthesisScratch scratch,
        double* aperiodicResponse, ref RandnState randnState)
    {
        GetNoiseSpectrum(noiseSize, fftSize, scratch.ForwardRealFft, ref randnState);

        MinimumPhaseAnalysis minimumPhase = scratch.MinimumPhase;
        if (currentVuv != 0.0)
        {
            for (int i = 0; i <= minimumPhase.FftSize / 2; ++i)
            {
                minimumPhase.LogSpectrum[i] = Math.Log(spectrum[i] * aperiodicRatio[i]) / 2.0;
            }
        }
        else
        {
            for (int i = 0; i <= minimumPhase.FftSize / 2; ++i)
            {
                minimumPhase.LogSpectrum[i] = Math.Log(spectrum[i]) / 2.0;
            }
        }
        minimumPhase.GetMinimumPhaseSpectrum();

        InverseRealFft inverseRealFft = scratch.InverseRealFft;
        ForwardRealFft forwardRealFft = scratch.ForwardRealFft;
        for (int i = 0; i <= fftSize / 2; ++i)
        {
            inverseRealFft.Spectrum[i].Real =
                (minimumPhase.MinimumPhaseSpectrum[i].Real * forwardRealFft.Spectrum[i].Real) -
                (minimumPhase.MinimumPhaseSpectrum[i].Imaginary *
                    forwardRealFft.Spectrum[i].Imaginary);
            inverseRealFft.Spectrum[i].Imaginary =
                (minimumPhase.MinimumPhaseSpectrum[i].Real *
                    forwardRealFft.Spectrum[i].Imaginary) +
                (minimumPhase.MinimumPhaseSpectrum[i].Imaginary *
                    forwardRealFft.Spectrum[i].Real);
        }
        inverseRealFft.InverseFft.Execute();
        MatlabFunctions.FftShift(inverseRealFft.Waveform, fftSize, aperiodicResponse);
    }

    private static void RemoveDCComponent(double* periodicResponse, int fftSize,
        double* dcRemover, double* newPeriodicResponse)
    {
        double dcComponent = 0.0;
        for (int i = fftSize / 2; i < fftSize; ++i)
        {
            dcComponent += periodicResponse[i];
        }
        for (int i = 0; i < fftSize / 2; ++i)
        {
            newPeriodicResponse[i] = -dcComponent * dcRemover[i];
        }
        for (int i = fftSize / 2; i < fftSize; ++i)
        {
            newPeriodicResponse[i] -= dcComponent * dcRemover[i];
        }
    }

    private static void GetSpectrumWithFractionalTimeShift(int fftSize, double coefficient,
        in InverseRealFft inverseRealFft)
    {
        for (int i = 0; i <= fftSize / 2; ++i)
        {
            double re = inverseRealFft.Spectrum[i].Real;
            double im = inverseRealFft.Spectrum[i].Imaginary;
            double re2 = Math.Cos(coefficient * i);
            double im2 = Math.Sqrt(1.0 - (re2 * re2));

            inverseRealFft.Spectrum[i].Real = (re * re2) + (im * im2);
            inverseRealFft.Spectrum[i].Imaginary = (im * re2) - (re * im2);
        }
    }

    private static void GetPeriodicResponse(int fftSize, double* spectrum,
        double* aperiodicRatio, double currentVuv, in SynthesisScratch scratch,
        double fractionalTimeShift, int fs, double* periodicResponse)
    {
        if (currentVuv <= 0.5 || aperiodicRatio[0] > 0.999)
        {
            for (int i = 0; i < fftSize; ++i)
            {
                periodicResponse[i] = 0.0;
            }
            return;
        }

        MinimumPhaseAnalysis minimumPhase = scratch.MinimumPhase;
        for (int i = 0; i <= minimumPhase.FftSize / 2; ++i)
        {
            minimumPhase.LogSpectrum[i] =
                Math.Log((spectrum[i] * (1.0 - aperiodicRatio[i])) +
                WorldConstants.MySafeGuardMinimum) / 2.0;
        }
        minimumPhase.GetMinimumPhaseSpectrum();

        InverseRealFft inverseRealFft = scratch.InverseRealFft;
        for (int i = 0; i <= fftSize / 2; ++i)
        {
            inverseRealFft.Spectrum[i].Real = minimumPhase.MinimumPhaseSpectrum[i].Real;
            inverseRealFft.Spectrum[i].Imaginary = minimumPhase.MinimumPhaseSpectrum[i].Imaginary;
        }

        double coefficient = 2.0 * WorldConstants.Pi * fractionalTimeShift * fs / fftSize;
        GetSpectrumWithFractionalTimeShift(fftSize, coefficient, inverseRealFft);

        inverseRealFft.InverseFft.Execute();
        MatlabFunctions.FftShift(inverseRealFft.Waveform, fftSize, periodicResponse);
        RemoveDCComponent(periodicResponse, fftSize, scratch.DcRemover, periodicResponse);
    }

    private static void GetSpectralEnvelope(double currentTime, double framePeriod, int f0Length,
        double* spectrogram, int spectrumLength, int fftSize, double* spectralEnvelope)
    {
        int currentFrameFloor =
            WorldMath.MinInt(f0Length - 1, (int)Math.Floor(currentTime / framePeriod));
        int currentFrameCeil =
            WorldMath.MinInt(f0Length - 1, (int)Math.Ceiling(currentTime / framePeriod));
        double interpolation = (currentTime / framePeriod) - currentFrameFloor;

        double* floorRow = spectrogram + ((long)currentFrameFloor * spectrumLength);
        double* ceilRow = spectrogram + ((long)currentFrameCeil * spectrumLength);

        if (currentFrameFloor == currentFrameCeil)
        {
            for (int i = 0; i <= fftSize / 2; ++i)
            {
                spectralEnvelope[i] = Math.Abs(floorRow[i]);
            }
        }
        else
        {
            for (int i = 0; i <= fftSize / 2; ++i)
            {
                spectralEnvelope[i] = ((1.0 - interpolation) * Math.Abs(floorRow[i])) +
                    (interpolation * Math.Abs(ceilRow[i]));
            }
        }
    }

    private static void GetAperiodicRatio(double currentTime, double framePeriod, int f0Length,
        double* aperiodicity, int spectrumLength, int fftSize, double* aperiodicSpectrum)
    {
        int currentFrameFloor =
            WorldMath.MinInt(f0Length - 1, (int)Math.Floor(currentTime / framePeriod));
        int currentFrameCeil =
            WorldMath.MinInt(f0Length - 1, (int)Math.Ceiling(currentTime / framePeriod));
        double interpolation = (currentTime / framePeriod) - currentFrameFloor;

        double* floorRow = aperiodicity + ((long)currentFrameFloor * spectrumLength);
        double* ceilRow = aperiodicity + ((long)currentFrameCeil * spectrumLength);

        if (currentFrameFloor == currentFrameCeil)
        {
            for (int i = 0; i <= fftSize / 2; ++i)
            {
                double safe = WorldMath.GetSafeAperiodicity(floorRow[i]);
                aperiodicSpectrum[i] = safe * safe;
            }
        }
        else
        {
            for (int i = 0; i <= fftSize / 2; ++i)
            {
                double blended =
                    ((1.0 - interpolation) * WorldMath.GetSafeAperiodicity(floorRow[i])) +
                    (interpolation * WorldMath.GetSafeAperiodicity(ceilRow[i]));
                aperiodicSpectrum[i] = blended * blended;
            }
        }
    }

    private static void GetOneFrameSegment(double currentVuv, int noiseSize, double* spectrogram,
        int fftSize, double* aperiodicity, int spectrumLength, int f0Length, double framePeriod,
        double currentTime, double fractionalTimeShift, int fs, in SynthesisScratch scratch,
        ref RandnState randnState)
    {
        double* aperiodicResponse = scratch.AperiodicResponse;
        double* periodicResponse = scratch.PeriodicResponse;
        double* spectralEnvelope = scratch.SpectralEnvelope;
        double* aperiodicRatio = scratch.AperiodicRatio;

        GetSpectralEnvelope(currentTime, framePeriod, f0Length, spectrogram, spectrumLength,
            fftSize, spectralEnvelope);
        GetAperiodicRatio(currentTime, framePeriod, f0Length, aperiodicity, spectrumLength,
            fftSize, aperiodicRatio);

        GetPeriodicResponse(fftSize, spectralEnvelope, aperiodicRatio, currentVuv, scratch,
            fractionalTimeShift, fs, periodicResponse);

        GetAperiodicResponse(noiseSize, fftSize, spectralEnvelope, aperiodicRatio, currentVuv,
            scratch, aperiodicResponse, ref randnState);

        double sqrtNoiseSize = Math.Sqrt(noiseSize);
        double* response = scratch.ImpulseResponse;
        for (int i = 0; i < fftSize; ++i)
        {
            response[i] =
                ((periodicResponse[i] * sqrtNoiseSize) + aperiodicResponse[i]) / fftSize;
        }
    }

    private static void GetTemporalParametersForTimeBase(double* f0, int f0Length, int fs,
        int yLength, double framePeriod, double lowestF0, in SynthesisScratch scratch)
    {
        double* timeAxis = scratch.TimeAxis;
        double* coarseTimeAxis = scratch.CoarseTimeAxis;
        double* coarseF0 = scratch.CoarseF0;
        double* coarseVuv = scratch.CoarseVuv;

        for (int i = 0; i < yLength; ++i)
        {
            timeAxis[i] = i / (double)fs;
        }
        for (int i = 0; i < f0Length; ++i)
        {
            coarseTimeAxis[i] = i * framePeriod;
            coarseF0[i] = f0[i] < lowestF0 ? 0.0 : f0[i];
            coarseVuv[i] = coarseF0[i] == 0.0 ? 0.0 : 1.0;
        }
        coarseTimeAxis[f0Length] = f0Length * framePeriod;
        coarseF0[f0Length] = (coarseF0[f0Length - 1] * 2) - coarseF0[f0Length - 2];
        coarseVuv[f0Length] = (coarseVuv[f0Length - 1] * 2) - coarseVuv[f0Length - 2];
    }

    private static int GetPulseLocationsForTimeBase(double* interpolatedF0, int yLength, int fs,
        in SynthesisScratch scratch)
    {
        double* totalPhase = scratch.TotalPhase;
        double* wrapPhase = scratch.WrapPhase;
        double* wrapPhaseAbs = scratch.WrapPhaseAbs;
        double* timeAxis = scratch.TimeAxis;

        totalPhase[0] = 2.0 * WorldConstants.Pi * interpolatedF0[0] / fs;
        wrapPhase[0] = totalPhase[0] % (2.0 * WorldConstants.Pi);
        for (int i = 1; i < yLength; ++i)
        {
            totalPhase[i] = totalPhase[i - 1] +
                (2.0 * WorldConstants.Pi * interpolatedF0[i] / fs);
            wrapPhase[i] = totalPhase[i] % (2.0 * WorldConstants.Pi);
            wrapPhaseAbs[i - 1] = Math.Abs(wrapPhase[i] - wrapPhase[i - 1]);
        }

        int numberOfPulses = 0;
        for (int i = 0; i < yLength - 1; ++i)
        {
            if (wrapPhaseAbs[i] > WorldConstants.Pi)
            {
                scratch.PulseLocations[numberOfPulses] = timeAxis[i];
                scratch.PulseLocationsIndex[numberOfPulses] = i;

                double y1 = wrapPhase[i] - (2.0 * WorldConstants.Pi);
                double y2 = wrapPhase[i + 1];
                double x = -y1 / (y2 - y1);
                scratch.PulseLocationsTimeShift[numberOfPulses] = x / fs;

                ++numberOfPulses;
            }
        }

        return numberOfPulses;
    }

    private static int GetTimeBase(double* f0, int f0Length, int fs, double framePeriod,
        int yLength, double lowestF0, in SynthesisScratch scratch)
    {
        GetTemporalParametersForTimeBase(f0, f0Length, fs, yLength, framePeriod, lowestF0,
            scratch);

        double* interpolatedF0 = scratch.InterpolatedF0;
        double* interpolatedVuv = scratch.InterpolatedVuv;
        MatlabFunctions.Interp1(scratch.CoarseTimeAxis, scratch.CoarseF0, f0Length + 1,
            scratch.TimeAxis, yLength, interpolatedF0, scratch.Interpolation);
        MatlabFunctions.Interp1(scratch.CoarseTimeAxis, scratch.CoarseVuv, f0Length + 1,
            scratch.TimeAxis, yLength, interpolatedVuv, scratch.Interpolation);

        for (int i = 0; i < yLength; ++i)
        {
            interpolatedVuv[i] = interpolatedVuv[i] > 0.5 ? 1.0 : 0.0;
            interpolatedF0[i] =
                interpolatedVuv[i] == 0.0 ? WorldConstants.DefaultF0 : interpolatedF0[i];
        }

        return GetPulseLocationsForTimeBase(interpolatedF0, yLength, fs, scratch);
    }

    private static void GetDCRemover(int fftSize, double* dcRemover)
    {
        double dcComponent = 0.0;
        for (int i = 0; i < fftSize / 2; ++i)
        {
            dcRemover[i] = 0.5 -
                (0.5 * Math.Cos(2.0 * WorldConstants.Pi * (i + 1.0) / (1.0 + fftSize)));
            dcRemover[fftSize - i - 1] = dcRemover[i];
            dcComponent += dcRemover[i] * 2.0;
        }
        for (int i = 0; i < fftSize / 2; ++i)
        {
            dcRemover[i] /= dcComponent;
            dcRemover[fftSize - i - 1] = dcRemover[i];
        }
    }
}
