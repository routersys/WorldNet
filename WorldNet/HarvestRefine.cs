using System.Runtime.CompilerServices;

namespace WorldNet;

public static unsafe partial class Harvest
{
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void GetBaseIndex(double currentPosition, double* baseTime, int baseTimeLength,
        double fs, int* baseIndex)
    {
        int basicIndex =
            MatlabFunctions.MatlabRound(((currentPosition + baseTime[0]) * fs) + 0.001);

        for (int i = 0; i < baseTimeLength; ++i)
        {
            baseIndex[i] = basicIndex + i;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void GetMainWindow(double currentPosition, int* baseIndex, int baseTimeLength,
        double fs, double windowLengthInTime, double* mainWindow)
    {
        for (int i = 0; i < baseTimeLength; ++i)
        {
            double tmp = ((baseIndex[i] - 1.0) / fs) - currentPosition;
            mainWindow[i] = 0.42 +
                (0.5 * Math.Cos(2.0 * WorldConstants.Pi * tmp / windowLengthInTime)) +
                (0.08 * Math.Cos(4.0 * WorldConstants.Pi * tmp / windowLengthInTime));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void GetDiffWindow(double* mainWindow, int baseTimeLength, double* diffWindow)
    {
        diffWindow[0] = -mainWindow[1] / 2.0;
        for (int i = 1; i < baseTimeLength - 1; ++i)
        {
            diffWindow[i] = -(mainWindow[i + 1] - mainWindow[i - 1]) / 2.0;
        }
        diffWindow[baseTimeLength - 1] = mainWindow[baseTimeLength - 2] / 2.0;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void GetSpectra(double* x, int xLength, int fftSize, int* baseIndex,
        double* mainWindow, double* diffWindow, int baseTimeLength,
        in ForwardRealFft forwardRealFft, FftComplex* mainSpectrum, FftComplex* diffSpectrum,
        int* safeIndex)
    {
        for (int i = 0; i < baseTimeLength; ++i)
        {
            safeIndex[i] = WorldMath.MaxInt(0, WorldMath.MinInt(xLength - 1, baseIndex[i] - 1));
        }
        for (int i = 0; i < baseTimeLength; ++i)
        {
            forwardRealFft.Waveform[i] = x[safeIndex[i]] * mainWindow[i];
        }
        new Span<double>(forwardRealFft.Waveform + baseTimeLength, fftSize - baseTimeLength).Clear();

        forwardRealFft.ForwardFft.Execute();
        Buffer.MemoryCopy(forwardRealFft.Spectrum, mainSpectrum,
            ((long)(fftSize / 2) + 1) * sizeof(FftComplex),
            ((long)(fftSize / 2) + 1) * sizeof(FftComplex));

        for (int i = 0; i < baseTimeLength; ++i)
        {
            forwardRealFft.Waveform[i] = x[safeIndex[i]] * diffWindow[i];
        }
        new Span<double>(forwardRealFft.Waveform + baseTimeLength, fftSize - baseTimeLength).Clear();
        forwardRealFft.ForwardFft.Execute();
        Buffer.MemoryCopy(forwardRealFft.Spectrum, diffSpectrum,
            ((long)(fftSize / 2) + 1) * sizeof(FftComplex),
            ((long)(fftSize / 2) + 1) * sizeof(FftComplex));
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void FixF0(double* powerSpectrum, double* numeratorI, int fftSize, double fs,
        double currentF0, int numberOfHarmonics, double* refinedF0, double* score,
        double* amplitudeList, double* instantaneousFrequencyList)
    {
        for (int i = 0; i < numberOfHarmonics; ++i)
        {
            int index = MatlabFunctions.MatlabRound(currentF0 * fftSize / fs * (i + 1));
            instantaneousFrequencyList[i] = powerSpectrum[index] == 0.0 ? 0.0 :
                ((double)index * fs / fftSize) +
                (numeratorI[index] / powerSpectrum[index] * fs / 2.0 / WorldConstants.Pi);
            amplitudeList[i] = Math.Sqrt(powerSpectrum[index]);
        }
        double denominator = 0.0;
        double numerator = 0.0;
        *score = 0.0;
        for (int i = 0; i < numberOfHarmonics; ++i)
        {
            numerator += amplitudeList[i] * instantaneousFrequencyList[i];
            denominator += amplitudeList[i] * (i + 1.0);
            *score += Math.Abs(((instantaneousFrequencyList[i] / (i + 1.0)) - currentF0) /
                currentF0);
        }

        *refinedF0 = numerator / (denominator + WorldConstants.MySafeGuardMinimum);
        *score = 1.0 / ((*score / numberOfHarmonics) + WorldConstants.MySafeGuardMinimum);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void GetSpectralTerms(double* x, int xLength, double fs,
        double currentPosition, int fftSize, double windowLengthInTime, double* baseTime,
        int baseTimeLength, in HarvestRefineScratch scratch)
    {
        FftComplex* mainSpectrum = scratch.MainSpectrum;
        FftComplex* diffSpectrum = scratch.DiffSpectrum;
        int* baseIndex = scratch.BaseIndex;
        double* mainWindow = scratch.MainWindow;
        double* diffWindow = scratch.DiffWindow;

        GetBaseIndex(currentPosition, baseTime, baseTimeLength, fs, baseIndex);
        GetMainWindow(currentPosition, baseIndex, baseTimeLength, fs, windowLengthInTime,
            mainWindow);
        GetDiffWindow(mainWindow, baseTimeLength, diffWindow);

        GetSpectra(x, xLength, fftSize, baseIndex, mainWindow, diffWindow, baseTimeLength,
            scratch.ForwardRealFft, mainSpectrum, diffSpectrum, scratch.SafeIndex);

        SpectrumMath.PowerSpectrumAndCrossProduct(mainSpectrum, diffSpectrum,
            scratch.PowerSpectrum, scratch.NumeratorI, (fftSize / 2) + 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void GetMeanF0(double fs, double currentF0, int fftSize, double* refinedF0,
        double* refinedScore, in HarvestRefineScratch scratch)
    {
        int numberOfHarmonics = WorldMath.MinInt((int)(fs / 2.0 / currentF0), 6);
        FixF0(scratch.PowerSpectrum, scratch.NumeratorI, fftSize, fs, currentF0,
            numberOfHarmonics, refinedF0, refinedScore, scratch.AmplitudeList,
            scratch.InstantaneousFrequencyList);
    }

    private const int MaximumRefineSizes = 32;

    private static int GetRefineFftSize(int halfWindowLength)
    {
        return (int)Math.Pow(2.0,
            2.0 + (int)(Math.Log((halfWindowLength * 2.0) + 1.0) / WorldConstants.Log2));
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void GetRefinedF0(double* x, int xLength, double fs, double currentPosition,
        double currentF0, double f0Floor, double f0Ceil, double* refinedF0, double* refinedScore,
        HarvestRefineScratch* scratches, int* sizes, int distinct, ref int cachedHalfWindowLength)
    {
        int halfWindowLength = (int)((1.5 * fs / currentF0) + 1.0);
        double windowLengthInTime = ((2.0 * halfWindowLength) + 1.0) / fs;
        int baseTimeLength = (halfWindowLength * 2) + 1;
        int fftSize = GetRefineFftSize(halfWindowLength);

        int index = 0;
        while (index < distinct && sizes[index] != fftSize)
        {
            ++index;
        }

        HarvestRefineScratch scratch = scratches[index];
        if (halfWindowLength != cachedHalfWindowLength)
        {
            double* baseTime = scratch.BaseTime;
            for (int i = 0; i < baseTimeLength; i++)
            {
                baseTime[i] = (-halfWindowLength + i) / fs;
            }

            GetSpectralTerms(x, xLength, fs, currentPosition, fftSize, windowLengthInTime,
                baseTime, baseTimeLength, scratch);
            cachedHalfWindowLength = halfWindowLength;
        }

        GetMeanF0(fs, currentF0, fftSize, refinedF0, refinedScore, scratch);

        if (*refinedF0 < f0Floor || *refinedF0 > f0Ceil || *refinedScore < 2.5)
        {
            *refinedF0 = 0.0;
            *refinedScore = 0.0;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void RefineF0Candidates(double* x, int xLength, double fs,
        double* temporalPositions, int f0Length, int maxCandidates, double f0Floor, double f0Ceil,
        double** refinedF0Candidates, double** f0Scores, WorldArena arena)
    {
        using WorldArenaScope scope = arena.BeginScope();

        int* sizes = (int*)arena.AllocateRaw(MaximumRefineSizes, sizeof(int));
        int* lengths = (int*)arena.AllocateRaw(MaximumRefineSizes, sizeof(int));
        int distinct = 0;

        for (int i = 0; i < f0Length; i++)
        {
            for (int j = 0; j < maxCandidates; ++j)
            {
                double currentF0 = refinedF0Candidates[i][j];
                if (currentF0 <= 0.0)
                {
                    continue;
                }
                int halfWindowLength = (int)((1.5 * fs / currentF0) + 1.0);
                int baseTimeLength = (halfWindowLength * 2) + 1;
                int fftSize = GetRefineFftSize(halfWindowLength);

                int index = 0;
                while (index < distinct && sizes[index] != fftSize)
                {
                    ++index;
                }
                if (index == distinct)
                {
                    if (distinct == MaximumRefineSizes)
                    {
                        throw new InvalidOperationException(
                            "The refinement stage needs more distinct FFT sizes than expected.");
                    }
                    sizes[distinct] = fftSize;
                    lengths[distinct] = baseTimeLength;
                    ++distinct;
                }
                else if (baseTimeLength > lengths[index])
                {
                    lengths[index] = baseTimeLength;
                }
            }
        }

        HarvestRefineScratch* scratches = (HarvestRefineScratch*)arena.AllocateRaw(
            distinct, (nuint)sizeof(HarvestRefineScratch));
        for (int i = 0; i < distinct; ++i)
        {
            scratches[i] = HarvestRefineScratch.Bind(arena, sizes[i], lengths[i]);
        }

        int* order = (int*)arena.AllocateRaw(maxCandidates, sizeof(int));
        int* keys = (int*)arena.AllocateRaw(maxCandidates, sizeof(int));

        for (int i = 0; i < f0Length; i++)
        {
            int count = 0;
            for (int j = 0; j < maxCandidates; ++j)
            {
                double currentF0 = refinedF0Candidates[i][j];
                if (currentF0 <= 0.0)
                {
                    refinedF0Candidates[i][j] = 0.0;
                    f0Scores[i][j] = 0.0;
                    continue;
                }

                keys[j] = (int)((1.5 * fs / currentF0) + 1.0);
                int position = count++;
                while (position > 0 && keys[order[position - 1]] > keys[j])
                {
                    order[position] = order[position - 1];
                    --position;
                }
                order[position] = j;
            }

            int cachedHalfWindowLength = -1;
            for (int k = 0; k < count; ++k)
            {
                int j = order[k];
                GetRefinedF0(x, xLength, fs, temporalPositions[i], refinedF0Candidates[i][j],
                    f0Floor, f0Ceil, &refinedF0Candidates[i][j], &f0Scores[i][j], scratches,
                    sizes, distinct, ref cachedHalfWindowLength);
            }
        }
    }
}
