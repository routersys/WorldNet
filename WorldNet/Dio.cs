namespace WorldNet;

public static unsafe class Dio
{
    public static int GetSamplesForDio(int fs, int xLength, double framePeriod)
    {
        return (int)(1000.0 * xLength / fs / framePeriod) + 1;
    }

    public static void Estimate(ReadOnlySpan<double> x, int fs, DioOption option,
        Span<double> temporalPositions, Span<double> f0, WorldArena arena)
    {
        ArgumentNullException.ThrowIfNull(arena);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fs);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(option.FramePeriod);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(option.F0Floor);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(option.F0Ceil);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(option.ChannelsInOctave);

        if (x.IsEmpty)
        {
            throw new ArgumentException("The waveform must not be empty.", nameof(x));
        }

        int f0Length = GetSamplesForDio(fs, x.Length, option.FramePeriod);

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
            DioGeneralBody(xPointer, x.Length, fs, option.FramePeriod, option.F0Floor,
                option.F0Ceil, option.ChannelsInOctave, option.Speed, option.AllowedRange,
                positionPointer, f0Pointer, arena);
        }
    }

    private static void DesignLowCutFilter(int n, int fftSize, double* lowCutFilter)
    {
        for (int i = 1; i <= n; ++i)
        {
            lowCutFilter[i - 1] = 0.5 - (0.5 * Math.Cos(i * 2.0 * WorldConstants.Pi / (n + 1)));
        }
        for (int i = n; i < fftSize; ++i)
        {
            lowCutFilter[i] = 0.0;
        }
        double sumOfAmplitude = 0.0;
        for (int i = 0; i < n; ++i)
        {
            sumOfAmplitude += lowCutFilter[i];
        }
        for (int i = 0; i < n; ++i)
        {
            lowCutFilter[i] = -lowCutFilter[i] / sumOfAmplitude;
        }
        for (int i = 0; i < (n - 1) / 2; ++i)
        {
            lowCutFilter[fftSize - ((n - 1) / 2) + i] = lowCutFilter[i];
        }
        for (int i = 0; i < n; ++i)
        {
            lowCutFilter[i] = lowCutFilter[i + ((n - 1) / 2)];
        }
        lowCutFilter[0] += 1.0;
    }

    private static void GetSpectrumForEstimation(double* x, int xLength, int yLength,
        double actualFs, int fftSize, int decimationRatio, in DioScratch scratch)
    {
        double* y = scratch.SpectrumY;

        for (int i = 0; i < fftSize; ++i)
        {
            y[i] = 0.0;
        }

        if (decimationRatio != 1)
        {
            MatlabFunctions.Decimate(x, xLength, decimationRatio, y, scratch.Decimate);
        }
        else
        {
            for (int i = 0; i < xLength; ++i)
            {
                y[i] = x[i];
            }
        }

        double meanY = 0.0;
        for (int i = 0; i < yLength; ++i)
        {
            meanY += y[i];
        }
        meanY /= yLength;
        for (int i = 0; i < yLength; ++i)
        {
            y[i] -= meanY;
        }
        for (int i = yLength; i < fftSize; ++i)
        {
            y[i] = 0.0;
        }

        FftPlan forwardFft = scratch.SpectrumPlan;
        forwardFft.Execute();

        int cutoffInSample = MatlabFunctions.MatlabRound(actualFs / WorldConstants.CutOff);
        DesignLowCutFilter((cutoffInSample * 2) + 1, fftSize, y);

        FftComplex* filterSpectrum = scratch.FilterSpectrum;
        forwardFft.COut = filterSpectrum;
        forwardFft.Execute();

        FftComplex* ySpectrum = scratch.YSpectrum;
        for (int i = 0; i <= fftSize / 2; ++i)
        {
            double tmp = (ySpectrum[i].Real * filterSpectrum[i].Real)
                - (ySpectrum[i].Imaginary * filterSpectrum[i].Imaginary);
            ySpectrum[i].Imaginary = (ySpectrum[i].Real * filterSpectrum[i].Imaginary)
                + (ySpectrum[i].Imaginary * filterSpectrum[i].Real);
            ySpectrum[i].Real = tmp;
        }
    }

    private static void GetBestF0Contour(int f0Length, double** f0Candidates,
        double** f0Scores, int numberOfBands, double* bestF0Contour)
    {
        for (int i = 0; i < f0Length; ++i)
        {
            double tmp = f0Scores[0][i];
            bestF0Contour[i] = f0Candidates[0][i];
            for (int j = 1; j < numberOfBands; ++j)
            {
                if (tmp > f0Scores[j][i])
                {
                    tmp = f0Scores[j][i];
                    bestF0Contour[i] = f0Candidates[j][i];
                }
            }
        }
    }

    private static void FixStep1(double* bestF0Contour, int f0Length, int voiceRangeMinimum,
        double allowedRange, double* f0Step1, in DioScratch scratch)
    {
        double* f0Base = scratch.F0Base;
        for (int i = 0; i < voiceRangeMinimum; ++i)
        {
            f0Base[i] = 0.0;
        }
        for (int i = voiceRangeMinimum; i < f0Length - voiceRangeMinimum; ++i)
        {
            f0Base[i] = bestF0Contour[i];
        }
        for (int i = f0Length - voiceRangeMinimum; i < f0Length; ++i)
        {
            f0Base[i] = 0.0;
        }

        for (int i = 0; i < voiceRangeMinimum; ++i)
        {
            f0Step1[i] = 0.0;
        }
        for (int i = voiceRangeMinimum; i < f0Length; ++i)
        {
            f0Step1[i] = Math.Abs((f0Base[i] - f0Base[i - 1]) /
                (WorldConstants.MySafeGuardMinimum + f0Base[i])) <
                allowedRange ? f0Base[i] : 0.0;
        }
    }

    private static void FixStep2(double* f0Step1, int f0Length, int voiceRangeMinimum,
        double* f0Step2)
    {
        for (int i = 0; i < f0Length; ++i)
        {
            f0Step2[i] = f0Step1[i];
        }

        int center = (voiceRangeMinimum - 1) / 2;
        for (int i = center; i < f0Length - center; ++i)
        {
            for (int j = -center; j <= center; ++j)
            {
                if (f0Step1[i + j] == 0)
                {
                    f0Step2[i] = 0.0;
                    break;
                }
            }
        }
    }

    private static void GetNumberOfVoicedSections(double* f0, int f0Length, int* positiveIndex,
        int* negativeIndex, out int positiveCount, out int negativeCount)
    {
        positiveCount = negativeCount = 0;
        for (int i = 1; i < f0Length; ++i)
        {
            if (f0[i] == 0 && f0[i - 1] != 0)
            {
                negativeIndex[negativeCount++] = i - 1;
            }
            else if (f0[i - 1] == 0 && f0[i] != 0)
            {
                positiveIndex[positiveCount++] = i;
            }
        }
    }

    private static double SelectBestF0(double currentF0, double pastF0, double** f0Candidates,
        int numberOfCandidates, int targetIndex, double allowedRange)
    {
        double referenceF0 = ((currentF0 * 3.0) - pastF0) / 2.0;

        double minimumError = Math.Abs(referenceF0 - f0Candidates[0][targetIndex]);
        double bestF0 = f0Candidates[0][targetIndex];

        for (int i = 1; i < numberOfCandidates; ++i)
        {
            double currentError = Math.Abs(referenceF0 - f0Candidates[i][targetIndex]);
            if (currentError < minimumError)
            {
                minimumError = currentError;
                bestF0 = f0Candidates[i][targetIndex];
            }
        }
        if (Math.Abs(1.0 - (bestF0 / referenceF0)) > allowedRange)
        {
            return 0.0;
        }
        return bestF0;
    }

    private static void FixStep3(double* f0Step2, int f0Length, double** f0Candidates,
        int numberOfCandidates, double allowedRange, int* negativeIndex, int negativeCount,
        double* f0Step3)
    {
        for (int i = 0; i < f0Length; i++)
        {
            f0Step3[i] = f0Step2[i];
        }

        for (int i = 0; i < negativeCount; ++i)
        {
            int limit = i == negativeCount - 1 ? f0Length - 1 : negativeIndex[i + 1];
            for (int j = negativeIndex[i]; j < limit; ++j)
            {
                f0Step3[j + 1] = SelectBestF0(f0Step3[j], f0Step3[j - 1], f0Candidates,
                    numberOfCandidates, j + 1, allowedRange);
                if (f0Step3[j + 1] == 0)
                {
                    break;
                }
            }
        }
    }

    private static void FixStep4(double* f0Step3, int f0Length, double** f0Candidates,
        int numberOfCandidates, double allowedRange, int* positiveIndex, int positiveCount,
        double* f0Step4)
    {
        for (int i = 0; i < f0Length; ++i)
        {
            f0Step4[i] = f0Step3[i];
        }

        for (int i = positiveCount - 1; i >= 0; --i)
        {
            int limit = i == 0 ? 1 : positiveIndex[i - 1];
            for (int j = positiveIndex[i]; j > limit; --j)
            {
                f0Step4[j - 1] = SelectBestF0(f0Step4[j], f0Step4[j + 1], f0Candidates,
                    numberOfCandidates, j - 1, allowedRange);
                if (f0Step4[j - 1] == 0)
                {
                    break;
                }
            }
        }
    }

    private static void FixF0Contour(double framePeriod, int numberOfCandidates, int fs,
        double** f0Candidates, double* bestF0Contour, int f0Length, double f0Floor,
        double allowedRange, double* fixedF0Contour, in DioScratch scratch)
    {
        int voiceRangeMinimum = ((int)(0.5 + (1000.0 / framePeriod / f0Floor)) * 2) + 1;

        if (f0Length <= voiceRangeMinimum)
        {
            return;
        }

        double* f0Tmp1 = scratch.F0Tmp1;
        double* f0Tmp2 = scratch.F0Tmp2;

        FixStep1(bestF0Contour, f0Length, voiceRangeMinimum, allowedRange, f0Tmp1, scratch);
        FixStep2(f0Tmp1, f0Length, voiceRangeMinimum, f0Tmp2);

        int* positiveIndex = scratch.PositiveIndex;
        int* negativeIndex = scratch.NegativeIndex;
        GetNumberOfVoicedSections(f0Tmp2, f0Length, positiveIndex, negativeIndex,
            out int positiveCount, out int negativeCount);
        FixStep3(f0Tmp2, f0Length, f0Candidates, numberOfCandidates, allowedRange,
            negativeIndex, negativeCount, f0Tmp1);
        FixStep4(f0Tmp1, f0Length, f0Candidates, numberOfCandidates, allowedRange,
            positiveIndex, positiveCount, fixedF0Contour);
    }

    private static void GetFilteredSignal(int halfAverageLength, int fftSize, int yLength,
        in DioScratch scratch)
    {
        double* lowPassFilter = scratch.LowPassFilter;
        Common.NuttallWindow(halfAverageLength * 4, lowPassFilter);
        for (int i = halfAverageLength * 4; i < fftSize; ++i)
        {
            lowPassFilter[i] = 0.0;
        }

        FftComplex* lowPassFilterSpectrum = scratch.LowPassFilterSpectrum;
        scratch.FilterForwardPlan.Execute();

        FftComplex* ySpectrum = scratch.YSpectrum;
        double tmp = (ySpectrum[0].Real * lowPassFilterSpectrum[0].Real)
            - (ySpectrum[0].Imaginary * lowPassFilterSpectrum[0].Imaginary);
        lowPassFilterSpectrum[0].Imaginary = (ySpectrum[0].Real * lowPassFilterSpectrum[0].Imaginary)
            + (ySpectrum[0].Imaginary * lowPassFilterSpectrum[0].Real);
        lowPassFilterSpectrum[0].Real = tmp;
        for (int i = 1; i <= fftSize / 2; ++i)
        {
            tmp = (ySpectrum[i].Real * lowPassFilterSpectrum[i].Real)
                - (ySpectrum[i].Imaginary * lowPassFilterSpectrum[i].Imaginary);
            lowPassFilterSpectrum[i].Imaginary = (ySpectrum[i].Real * lowPassFilterSpectrum[i].Imaginary)
                + (ySpectrum[i].Imaginary * lowPassFilterSpectrum[i].Real);
            lowPassFilterSpectrum[i].Real = tmp;
            lowPassFilterSpectrum[fftSize - i - 1].Real = lowPassFilterSpectrum[i].Real;
            lowPassFilterSpectrum[fftSize - i - 1].Imaginary = lowPassFilterSpectrum[i].Imaginary;
        }

        scratch.FilterInversePlan.Execute();

        double* filteredSignal = scratch.FilteredSignal;
        int indexBias = halfAverageLength * 2;
        for (int i = 0; i < yLength; ++i)
        {
            filteredSignal[i] = filteredSignal[i + indexBias];
        }
    }

    private static int CheckEvent(int x)
    {
        return x > 0 ? 1 : 0;
    }

    private static int ZeroCrossingEngine(double* filteredSignal, int yLength, double fs,
        double* intervalLocations, double* intervals, int* negativeGoingPoints, int* edges,
        double* fineEdges)
    {
        if (yLength < 2)
        {
            return 0;
        }

        for (int i = 0; i < yLength - 1; ++i)
        {
            negativeGoingPoints[i] =
                0.0 < filteredSignal[i] && filteredSignal[i + 1] <= 0.0 ? i + 1 : 0;
        }
        negativeGoingPoints[yLength - 1] = 0;

        int count = 0;
        for (int i = 0; i < yLength; ++i)
        {
            if (negativeGoingPoints[i] > 0)
            {
                edges[count++] = negativeGoingPoints[i];
            }
        }

        if (count < 2)
        {
            return 0;
        }

        for (int i = 0; i < count; ++i)
        {
            fineEdges[i] = edges[i] - (filteredSignal[edges[i] - 1] /
                (filteredSignal[edges[i]] - filteredSignal[edges[i] - 1]));
        }

        for (int i = 0; i < count - 1; ++i)
        {
            intervals[i] = fs / (fineEdges[i + 1] - fineEdges[i]);
            intervalLocations[i] = (fineEdges[i] + fineEdges[i + 1]) / 2.0 / fs;
        }
        return count - 1;
    }

    private static void GetFourZeroCrossingIntervals(double* filteredSignal, int yLength,
        double actualFs, ref ZeroCrossings zeroCrossings)
    {
        zeroCrossings.NumberOfNegatives = ZeroCrossingEngine(filteredSignal, yLength, actualFs,
            zeroCrossings.NegativeIntervalLocations, zeroCrossings.NegativeIntervals,
            zeroCrossings.NegativeGoingPoints, zeroCrossings.Edges, zeroCrossings.FineEdges);

        for (int i = 0; i < yLength; ++i)
        {
            filteredSignal[i] = -filteredSignal[i];
        }
        zeroCrossings.NumberOfPositives = ZeroCrossingEngine(filteredSignal, yLength, actualFs,
            zeroCrossings.PositiveIntervalLocations, zeroCrossings.PositiveIntervals,
            zeroCrossings.NegativeGoingPoints, zeroCrossings.Edges, zeroCrossings.FineEdges);

        for (int i = 0; i < yLength - 1; ++i)
        {
            filteredSignal[i] = filteredSignal[i] - filteredSignal[i + 1];
        }
        zeroCrossings.NumberOfPeaks = ZeroCrossingEngine(filteredSignal, yLength - 1, actualFs,
            zeroCrossings.PeakIntervalLocations, zeroCrossings.PeakIntervals,
            zeroCrossings.NegativeGoingPoints, zeroCrossings.Edges, zeroCrossings.FineEdges);

        for (int i = 0; i < yLength - 1; ++i)
        {
            filteredSignal[i] = -filteredSignal[i];
        }
        zeroCrossings.NumberOfDips = ZeroCrossingEngine(filteredSignal, yLength - 1, actualFs,
            zeroCrossings.DipIntervalLocations, zeroCrossings.DipIntervals,
            zeroCrossings.NegativeGoingPoints, zeroCrossings.Edges, zeroCrossings.FineEdges);
    }

    private static void GetF0CandidateContourSub(double** interpolatedF0Set, int f0Length,
        double f0Floor, double f0Ceil, double boundaryF0, double* f0Candidate, double* f0Score)
    {
        for (int i = 0; i < f0Length; ++i)
        {
            f0Candidate[i] = (interpolatedF0Set[0][i] + interpolatedF0Set[1][i] +
                interpolatedF0Set[2][i] + interpolatedF0Set[3][i]) / 4.0;

            f0Score[i] = Math.Sqrt(((interpolatedF0Set[0][i] - f0Candidate[i]) *
                (interpolatedF0Set[0][i] - f0Candidate[i]) +
                (interpolatedF0Set[1][i] - f0Candidate[i]) *
                (interpolatedF0Set[1][i] - f0Candidate[i]) +
                (interpolatedF0Set[2][i] - f0Candidate[i]) *
                (interpolatedF0Set[2][i] - f0Candidate[i]) +
                (interpolatedF0Set[3][i] - f0Candidate[i]) *
                (interpolatedF0Set[3][i] - f0Candidate[i])) / 3.0);

            if (f0Candidate[i] > boundaryF0 || f0Candidate[i] < boundaryF0 / 2.0 ||
                f0Candidate[i] > f0Ceil || f0Candidate[i] < f0Floor)
            {
                f0Candidate[i] = 0.0;
                f0Score[i] = WorldConstants.MaximumValue;
            }
        }
    }

    private static void GetF0CandidateContour(in ZeroCrossings zeroCrossings, double boundaryF0,
        double f0Floor, double f0Ceil, double* temporalPositions, int f0Length,
        double* f0Candidate, double* f0Score, in DioScratch scratch)
    {
        if (0 == CheckEvent(zeroCrossings.NumberOfNegatives - 2) *
            CheckEvent(zeroCrossings.NumberOfPositives - 2) *
            CheckEvent(zeroCrossings.NumberOfPeaks - 2) *
            CheckEvent(zeroCrossings.NumberOfDips - 2))
        {
            for (int i = 0; i < f0Length; ++i)
            {
                f0Score[i] = WorldConstants.MaximumValue;
                f0Candidate[i] = 0.0;
            }
            return;
        }

        double** interpolatedF0Set = scratch.InterpolatedF0Set;

        MatlabFunctions.Interp1(zeroCrossings.NegativeIntervalLocations,
            zeroCrossings.NegativeIntervals, zeroCrossings.NumberOfNegatives,
            temporalPositions, f0Length, interpolatedF0Set[0], scratch.Interpolation);
        MatlabFunctions.Interp1(zeroCrossings.PositiveIntervalLocations,
            zeroCrossings.PositiveIntervals, zeroCrossings.NumberOfPositives,
            temporalPositions, f0Length, interpolatedF0Set[1], scratch.Interpolation);
        MatlabFunctions.Interp1(zeroCrossings.PeakIntervalLocations,
            zeroCrossings.PeakIntervals, zeroCrossings.NumberOfPeaks,
            temporalPositions, f0Length, interpolatedF0Set[2], scratch.Interpolation);
        MatlabFunctions.Interp1(zeroCrossings.DipIntervalLocations,
            zeroCrossings.DipIntervals, zeroCrossings.NumberOfDips,
            temporalPositions, f0Length, interpolatedF0Set[3], scratch.Interpolation);

        GetF0CandidateContourSub(interpolatedF0Set, f0Length, f0Floor, f0Ceil, boundaryF0,
            f0Candidate, f0Score);
    }

    private static void GetF0CandidateFromRawEvent(double boundaryF0, double fs, int yLength,
        int fftSize, double f0Floor, double f0Ceil, double* temporalPositions, int f0Length,
        double* f0Score, double* f0Candidate, in DioScratch scratch)
    {
        double* filteredSignal = scratch.FilteredSignal;
        GetFilteredSignal(MatlabFunctions.MatlabRound(fs / boundaryF0 / 2.0), fftSize, yLength,
            scratch);

        ZeroCrossings zeroCrossings = scratch.ZeroCrossings;
        GetFourZeroCrossingIntervals(filteredSignal, yLength, fs, ref zeroCrossings);

        GetF0CandidateContour(zeroCrossings, boundaryF0, f0Floor, f0Ceil, temporalPositions,
            f0Length, f0Candidate, f0Score, scratch);
    }

    private static void GetF0CandidatesAndScores(double* boundaryF0List, int numberOfBands,
        double actualFs, int yLength, double* temporalPositions, int f0Length, int fftSize,
        double f0Floor, double f0Ceil, double** rawF0Candidates, double** rawF0Scores,
        in DioScratch scratch)
    {
        double* f0Candidate = scratch.F0Candidate;
        double* f0Score = scratch.F0Score;

        for (int i = 0; i < numberOfBands; ++i)
        {
            GetF0CandidateFromRawEvent(boundaryF0List[i], actualFs, yLength, fftSize, f0Floor,
                f0Ceil, temporalPositions, f0Length, f0Score, f0Candidate, scratch);
            for (int j = 0; j < f0Length; ++j)
            {
                rawF0Scores[i][j] = f0Score[j] / (f0Candidate[j] + WorldConstants.MySafeGuardMinimum);
                rawF0Candidates[i][j] = f0Candidate[j];
            }
        }
    }

    private static void DioGeneralBody(double* x, int xLength, int fs, double framePeriod,
        double f0Floor, double f0Ceil, double channelsInOctave, int speed, double allowedRange,
        double* temporalPositions, double* f0, WorldArena arena)
    {
        using WorldArenaScope scope = arena.BeginScope();

        int numberOfBands = 1 + (int)(Math.Log(f0Ceil / f0Floor) / WorldConstants.Log2 *
            channelsInOctave);
        double* boundaryF0List = (double*)arena.AllocateRaw(numberOfBands, sizeof(double));
        for (int i = 0; i < numberOfBands; ++i)
        {
            boundaryF0List[i] = f0Floor * Math.Pow(2.0, (i + 1) / channelsInOctave);
        }

        int decimationRatio = WorldMath.MaxInt(WorldMath.MinInt(speed, 12), 1);
        int yLength = 1 + (xLength / decimationRatio);
        double actualFs = (double)fs / decimationRatio;
        int fftSize = Common.GetSuitableFftSize(yLength +
            (MatlabFunctions.MatlabRound(actualFs / WorldConstants.CutOff) * 2) + 1 +
            (4 * (int)(1.0 + (actualFs / boundaryF0List[0] / 2.0))));

        int f0Length = GetSamplesForDio(fs, xLength, framePeriod);

        DioScratch scratch = DioScratch.Bind(arena, numberOfBands, xLength, yLength, f0Length,
            fftSize);

        GetSpectrumForEstimation(x, xLength, yLength, actualFs, fftSize, decimationRatio, scratch);

        for (int i = 0; i < f0Length; ++i)
        {
            temporalPositions[i] = i * framePeriod / 1000.0;
        }

        GetF0CandidatesAndScores(boundaryF0List, numberOfBands, actualFs, yLength,
            temporalPositions, f0Length, fftSize, f0Floor, f0Ceil, scratch.F0Candidates,
            scratch.F0Scores, scratch);

        double* bestF0Contour = scratch.BestF0Contour;
        GetBestF0Contour(f0Length, scratch.F0Candidates, scratch.F0Scores, numberOfBands,
            bestF0Contour);

        FixF0Contour(framePeriod, numberOfBands, fs, scratch.F0Candidates, bestF0Contour,
            f0Length, f0Floor, allowedRange, f0, scratch);
    }
}
