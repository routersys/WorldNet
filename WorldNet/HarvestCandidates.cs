namespace WorldNet;

public static unsafe partial class Harvest
{
    private static void GetWaveformAndSpectrumSub(double* x, int xLength, int yLength,
        int decimationRatio, in HarvestScratch scratch)
    {
        double* y = scratch.Y;
        if (decimationRatio == 1)
        {
            for (int i = 0; i < xLength; ++i)
            {
                y[i] = x[i];
            }
            return;
        }

        int lag = (int)(Math.Ceiling(140.0 / decimationRatio) * decimationRatio);
        int newXLength = xLength + (lag * 2);
        double* newY = scratch.NewY;
        for (int i = 0; i < newXLength; ++i)
        {
            newY[i] = 0.0;
        }
        double* newX = scratch.NewX;
        for (int i = 0; i < lag; ++i)
        {
            newX[i] = x[0];
        }
        for (int i = lag; i < lag + xLength; ++i)
        {
            newX[i] = x[i - lag];
        }
        for (int i = lag + xLength; i < newXLength; ++i)
        {
            newX[i] = x[xLength - 1];
        }

        MatlabFunctions.Decimate(newX, newXLength, decimationRatio, newY, scratch.WaveformDecimate);
        for (int i = 0; i < yLength; ++i)
        {
            y[i] = newY[(lag / decimationRatio) + i];
        }
    }

    private static void GetWaveformAndSpectrum(double* x, int xLength, int yLength, int fftSize,
        int decimationRatio, in HarvestScratch scratch)
    {
        double* y = scratch.Y;
        for (int i = 0; i < fftSize; ++i)
        {
            y[i] = 0.0;
        }

        GetWaveformAndSpectrumSub(x, xLength, yLength, decimationRatio, scratch);

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

        scratch.WaveformPlan.Execute();
    }

    private static void GetFilteredSignal(double boundaryF0, int fftSize, double fs, int yLength,
        in HarvestScratch scratch)
    {
        int filterLengthHalf = MatlabFunctions.MatlabRound(fs / boundaryF0 * 2.0);
        double* bandPassFilter = scratch.BandPassFilter;
        Common.NuttallWindow((filterLengthHalf * 2) + 1, bandPassFilter);
        for (int i = -filterLengthHalf; i <= filterLengthHalf; ++i)
        {
            bandPassFilter[i + filterLengthHalf] *=
                Math.Cos(2 * WorldConstants.Pi * boundaryF0 * i / fs);
        }
        for (int i = (filterLengthHalf * 2) + 1; i < fftSize; ++i)
        {
            bandPassFilter[i] = 0.0;
        }

        FftComplex* bandPassFilterSpectrum = scratch.BandPassFilterSpectrum;
        scratch.FilterForwardPlan.Execute();

        FftComplex* ySpectrum = scratch.YSpectrum;
        double tmp = (ySpectrum[0].Real * bandPassFilterSpectrum[0].Real)
            - (ySpectrum[0].Imaginary * bandPassFilterSpectrum[0].Imaginary);
        bandPassFilterSpectrum[0].Imaginary = (ySpectrum[0].Real * bandPassFilterSpectrum[0].Imaginary)
            + (ySpectrum[0].Imaginary * bandPassFilterSpectrum[0].Real);
        bandPassFilterSpectrum[0].Real = tmp;
        for (int i = 1; i <= fftSize / 2; ++i)
        {
            tmp = (ySpectrum[i].Real * bandPassFilterSpectrum[i].Real)
                - (ySpectrum[i].Imaginary * bandPassFilterSpectrum[i].Imaginary);
            bandPassFilterSpectrum[i].Imaginary = (ySpectrum[i].Real * bandPassFilterSpectrum[i].Imaginary)
                + (ySpectrum[i].Imaginary * bandPassFilterSpectrum[i].Real);
            bandPassFilterSpectrum[i].Real = tmp;
            bandPassFilterSpectrum[fftSize - i - 1].Real = bandPassFilterSpectrum[i].Real;
            bandPassFilterSpectrum[fftSize - i - 1].Imaginary = bandPassFilterSpectrum[i].Imaginary;
        }

        scratch.FilterInversePlan.Execute();

        double* filteredSignal = scratch.FilteredSignal;
        int indexBias = filterLengthHalf + 1;
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
        double f0Floor, double f0Ceil, double boundaryF0, double* f0Candidate)
    {
        double upper = boundaryF0 * 1.1;
        double lower = boundaryF0 * 0.9;
        for (int i = 0; i < f0Length; ++i)
        {
            f0Candidate[i] = (interpolatedF0Set[0][i] + interpolatedF0Set[1][i] +
                interpolatedF0Set[2][i] + interpolatedF0Set[3][i]) / 4.0;

            if (f0Candidate[i] > upper || f0Candidate[i] < lower ||
                f0Candidate[i] > f0Ceil || f0Candidate[i] < f0Floor)
            {
                f0Candidate[i] = 0.0;
            }
        }
    }

    private static void GetF0CandidateContour(in ZeroCrossings zeroCrossings, double boundaryF0,
        double f0Floor, double f0Ceil, double* temporalPositions, int f0Length,
        double* f0Candidate, in HarvestScratch scratch)
    {
        if (0 == CheckEvent(zeroCrossings.NumberOfNegatives - 2) *
            CheckEvent(zeroCrossings.NumberOfPositives - 2) *
            CheckEvent(zeroCrossings.NumberOfPeaks - 2) *
            CheckEvent(zeroCrossings.NumberOfDips - 2))
        {
            for (int i = 0; i < f0Length; ++i)
            {
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
            f0Candidate);
    }

    private static void GetF0CandidateFromRawEvent(double boundaryF0, double fs, int yLength,
        int fftSize, double f0Floor, double f0Ceil, double* temporalPositions, int f0Length,
        double* f0Candidate, in HarvestScratch scratch)
    {
        double* filteredSignal = scratch.FilteredSignal;
        GetFilteredSignal(boundaryF0, fftSize, fs, yLength, scratch);

        ZeroCrossings zeroCrossings = scratch.ZeroCrossings;
        GetFourZeroCrossingIntervals(filteredSignal, yLength, fs, ref zeroCrossings);

        GetF0CandidateContour(zeroCrossings, boundaryF0, f0Floor, f0Ceil, temporalPositions,
            f0Length, f0Candidate, scratch);
    }

    private static void GetRawF0Candidates(double* boundaryF0List, int numberOfBands,
        double actualFs, int yLength, double* temporalPositions, int f0Length, int fftSize,
        double f0Floor, double f0Ceil, double** rawF0Candidates, in HarvestScratch scratch)
    {
        for (int i = 0; i < numberOfBands; ++i)
        {
            GetF0CandidateFromRawEvent(boundaryF0List[i], actualFs, yLength, fftSize, f0Floor,
                f0Ceil, temporalPositions, f0Length, rawF0Candidates[i], scratch);
        }
    }

    private static int DetectOfficialF0CandidatesSub1(int* vuv, int numberOfChannels, int* st,
        int* ed)
    {
        int numberOfVoicedSections = 0;
        for (int i = 1; i < numberOfChannels; ++i)
        {
            int tmp = vuv[i] - vuv[i - 1];
            if (tmp == 1)
            {
                st[numberOfVoicedSections] = i;
            }
            if (tmp == -1)
            {
                ed[numberOfVoicedSections++] = i;
            }
        }
        return numberOfVoicedSections;
    }

    private static int DetectOfficialF0CandidatesSub2(double** rawF0Candidates, int index,
        int numberOfVoicedSections, int* st, int* ed, int maxCandidates, double* f0List)
    {
        int numberOfCandidates = 0;
        for (int i = 0; i < numberOfVoicedSections; ++i)
        {
            if (ed[i] - st[i] < 10)
            {
                continue;
            }

            double tmpF0 = 0.0;
            for (int j = st[i]; j < ed[i]; ++j)
            {
                tmpF0 += rawF0Candidates[j][index];
            }
            tmpF0 /= ed[i] - st[i];
            f0List[numberOfCandidates++] = tmpF0;
        }

        for (int i = numberOfCandidates; i < maxCandidates; ++i)
        {
            f0List[i] = 0.0;
        }
        return numberOfCandidates;
    }

    private static int DetectOfficialF0Candidates(double** rawF0Candidates, int numberOfChannels,
        int f0Length, int maxCandidates, double** f0Candidates, in HarvestScratch scratch)
    {
        int numberOfCandidates = 0;

        int* vuv = scratch.Vuv;
        int* st = scratch.St;
        int* ed = scratch.Ed;
        for (int i = 0; i < f0Length; ++i)
        {
            for (int j = 0; j < numberOfChannels; ++j)
            {
                vuv[j] = rawF0Candidates[j][i] > 0 ? 1 : 0;
            }
            vuv[0] = vuv[numberOfChannels - 1] = 0;
            int numberOfVoicedSections =
                DetectOfficialF0CandidatesSub1(vuv, numberOfChannels, st, ed);
            numberOfCandidates = WorldMath.MaxInt(numberOfCandidates,
                DetectOfficialF0CandidatesSub2(rawF0Candidates, i, numberOfVoicedSections, st, ed,
                    maxCandidates, f0Candidates[i]));
        }

        return numberOfCandidates;
    }

    private static void OverlapF0Candidates(int f0Length, int numberOfCandidates,
        double** f0Candidates)
    {
        int n = 3;
        for (int i = 1; i <= n; ++i)
        {
            for (int j = 0; j < numberOfCandidates; ++j)
            {
                for (int k = i; k < f0Length; ++k)
                {
                    f0Candidates[k][j + (numberOfCandidates * i)] = f0Candidates[k - i][j];
                }
                for (int k = 0; k < f0Length - i; ++k)
                {
                    f0Candidates[k][j + (numberOfCandidates * (i + n))] = f0Candidates[k + i][j];
                }
            }
        }
    }
}
