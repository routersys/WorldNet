namespace WorldNet;

public static unsafe class Codec
{
    public static int GetNumberOfAperiodicities(int fs)
    {
        return (int)(WorldMath.MinDouble(WorldConstants.UpperLimit,
            (fs / 2.0) - WorldConstants.FrequencyInterval) / WorldConstants.FrequencyInterval);
    }

    public static void CodeAperiodicity(ReadOnlySpan<double> aperiodicity, int f0Length, int fs,
        int fftSize, Span<double> codedAperiodicity, WorldArena arena)
    {
        ArgumentNullException.ThrowIfNull(arena);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fs);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fftSize);
        ArgumentOutOfRangeException.ThrowIfNegative(f0Length);

        int spectrumLength = (fftSize / 2) + 1;
        int numberOfAperiodicities = GetNumberOfAperiodicities(fs);
        ThrowIfSmaller(aperiodicity.Length, (long)f0Length * spectrumLength, nameof(aperiodicity));
        ThrowIfSmaller(codedAperiodicity.Length, (long)f0Length * numberOfAperiodicities,
            nameof(codedAperiodicity));

        using WorldArenaScope scope = arena.BeginScope();
        double* coarseFrequencyAxis =
            (double*)arena.AllocateRaw(numberOfAperiodicities, sizeof(double));
        for (int i = 0; i < numberOfAperiodicities; ++i)
        {
            coarseFrequencyAxis[i] = WorldConstants.FrequencyInterval * (i + 1.0);
        }

        double* logAperiodicity = (double*)arena.AllocateRaw(spectrumLength, sizeof(double));
        Interp1QScratch interpolation =
            Interp1QScratch.Bind(arena, spectrumLength, numberOfAperiodicities);

        fixed (double* aperiodicityPointer = aperiodicity)
        fixed (double* codedPointer = codedAperiodicity)
        {
            for (int i = 0; i < f0Length; ++i)
            {
                double* row = aperiodicityPointer + ((long)i * spectrumLength);
                for (int j = 0; j < spectrumLength; ++j)
                {
                    logAperiodicity[j] = 20 * Math.Log10(row[j]);
                }
                MatlabFunctions.Interp1Q(0, (double)fs / fftSize, logAperiodicity, spectrumLength,
                    coarseFrequencyAxis, numberOfAperiodicities,
                    codedPointer + ((long)i * numberOfAperiodicities), interpolation);
            }
        }
    }

    public static void DecodeAperiodicity(ReadOnlySpan<double> codedAperiodicity, int f0Length,
        int fs, int fftSize, Span<double> aperiodicity, WorldArena arena)
    {
        ArgumentNullException.ThrowIfNull(arena);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fs);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fftSize);
        ArgumentOutOfRangeException.ThrowIfNegative(f0Length);

        int spectrumLength = (fftSize / 2) + 1;
        int numberOfAperiodicities = GetNumberOfAperiodicities(fs);
        ThrowIfSmaller(codedAperiodicity.Length, (long)f0Length * numberOfAperiodicities,
            nameof(codedAperiodicity));
        ThrowIfSmaller(aperiodicity.Length, (long)f0Length * spectrumLength, nameof(aperiodicity));

        using WorldArenaScope scope = arena.BeginScope();

        fixed (double* codedPointer = codedAperiodicity)
        fixed (double* aperiodicityPointer = aperiodicity)
        {
            InitializeAperiodicity(f0Length, fftSize, spectrumLength, aperiodicityPointer);

            double* frequencyAxis = (double*)arena.AllocateRaw(spectrumLength, sizeof(double));
            for (int i = 0; i <= fftSize / 2; ++i)
            {
                frequencyAxis[i] = (double)fs / fftSize * i;
            }

            double* coarseFrequencyAxis =
                (double*)arena.AllocateRaw(numberOfAperiodicities + 2, sizeof(double));
            for (int i = 0; i <= numberOfAperiodicities; ++i)
            {
                coarseFrequencyAxis[i] = i * WorldConstants.FrequencyInterval;
            }
            coarseFrequencyAxis[numberOfAperiodicities + 1] = fs / 2.0;

            double* coarseAperiodicity =
                (double*)arena.AllocateRaw(numberOfAperiodicities + 2, sizeof(double));
            coarseAperiodicity[0] = -60.0;
            coarseAperiodicity[numberOfAperiodicities + 1] = -WorldConstants.MySafeGuardMinimum;

            Interp1Scratch interpolation =
                Interp1Scratch.Bind(arena, numberOfAperiodicities + 2, spectrumLength);

            for (int i = 0; i < f0Length; ++i)
            {
                double* codedRow = codedPointer + ((long)i * numberOfAperiodicities);
                if (CheckVUV(codedRow, numberOfAperiodicities, coarseAperiodicity) == 1)
                {
                    continue;
                }
                GetAperiodicity(coarseFrequencyAxis, coarseAperiodicity, numberOfAperiodicities,
                    frequencyAxis, fftSize,
                    aperiodicityPointer + ((long)i * spectrumLength), interpolation);
            }
        }
    }

    public static void CodeSpectralEnvelope(ReadOnlySpan<double> spectrogram, int f0Length,
        int fs, int fftSize, int numberOfDimensions, Span<double> codedSpectralEnvelope,
        WorldArena arena)
    {
        ArgumentNullException.ThrowIfNull(arena);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fs);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fftSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(numberOfDimensions);
        ArgumentOutOfRangeException.ThrowIfNegative(f0Length);

        int spectrumLength = (fftSize / 2) + 1;
        int maxDimension = fftSize / 2;
        ThrowIfSmaller(spectrogram.Length, (long)f0Length * spectrumLength, nameof(spectrogram));
        ThrowIfSmaller(codedSpectralEnvelope.Length, (long)f0Length * numberOfDimensions,
            nameof(codedSpectralEnvelope));

        using WorldArenaScope scope = arena.BeginScope();
        double* melAxis = (double*)arena.AllocateRaw(maxDimension, sizeof(double));
        double* frequencyAxis = (double*)arena.AllocateRaw(spectrumLength, sizeof(double));
        double* tmpSpectrum = (double*)arena.AllocateRaw(spectrumLength, sizeof(double));
        FftComplex* weight =
            (FftComplex*)arena.AllocateRaw(maxDimension, (nuint)sizeof(FftComplex));

        GetParametersForCoding(WorldConstants.FloorFrequency,
            WorldMath.MinDouble(fs / 2.0, WorldConstants.CeilFrequency), fs, fftSize, melAxis,
            frequencyAxis, weight);

        ForwardRealFft forwardRealFft = ForwardRealFft.Bind(arena, fftSize / 2);
        double* melSpectrum = (double*)arena.AllocateRaw(maxDimension, sizeof(double));
        Interp1Scratch interpolation = Interp1Scratch.Bind(arena, spectrumLength, maxDimension);

        fixed (double* spectrogramPointer = spectrogram)
        fixed (double* codedPointer = codedSpectralEnvelope)
        {
            for (int i = 0; i < f0Length; ++i)
            {
                double* row = spectrogramPointer + ((long)i * spectrumLength);
                for (int j = 0; j < spectrumLength; ++j)
                {
                    tmpSpectrum[j] = Math.Log(row[j]);
                }
                CodeOneFrame(tmpSpectrum, frequencyAxis, fftSize, melAxis, weight, maxDimension,
                    numberOfDimensions, forwardRealFft,
                    codedPointer + ((long)i * numberOfDimensions), melSpectrum, interpolation);
            }
        }
    }

    public static void DecodeSpectralEnvelope(ReadOnlySpan<double> codedSpectralEnvelope,
        int f0Length, int fs, int fftSize, int numberOfDimensions, Span<double> spectrogram,
        WorldArena arena)
    {
        ArgumentNullException.ThrowIfNull(arena);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fs);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fftSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(numberOfDimensions);
        ArgumentOutOfRangeException.ThrowIfNegative(f0Length);

        int spectrumLength = (fftSize / 2) + 1;
        int maxDimension = fftSize / 2;
        ThrowIfSmaller(codedSpectralEnvelope.Length, (long)f0Length * numberOfDimensions,
            nameof(codedSpectralEnvelope));
        ThrowIfSmaller(spectrogram.Length, (long)f0Length * spectrumLength, nameof(spectrogram));

        using WorldArenaScope scope = arena.BeginScope();
        double* melAxis = (double*)arena.AllocateRaw(maxDimension + 2, sizeof(double));
        double* frequencyAxis = (double*)arena.AllocateRaw(spectrumLength, sizeof(double));
        FftComplex* weight =
            (FftComplex*)arena.AllocateRaw(maxDimension, (nuint)sizeof(FftComplex));

        GetParametersForDecoding(WorldConstants.FloorFrequency,
            WorldMath.MinDouble(fs / 2.0, WorldConstants.CeilFrequency), fs, fftSize,
            numberOfDimensions, melAxis, frequencyAxis, weight);

        InverseComplexFft inverseComplexFft = InverseComplexFft.Bind(arena, fftSize / 2);
        double* melSpectrum = (double*)arena.AllocateRaw(maxDimension + 2, sizeof(double));
        Interp1Scratch interpolation =
            Interp1Scratch.Bind(arena, maxDimension + 2, spectrumLength);

        fixed (double* codedPointer = codedSpectralEnvelope)
        fixed (double* spectrogramPointer = spectrogram)
        {
            for (int i = 0; i < f0Length; ++i)
            {
                DecodeOneFrame(codedPointer + ((long)i * numberOfDimensions), frequencyAxis,
                    fftSize, melAxis, weight, maxDimension, numberOfDimensions, inverseComplexFft,
                    spectrogramPointer + ((long)i * spectrumLength), melSpectrum, interpolation);
            }
        }
    }

    private static void ThrowIfSmaller(long actual, long required, string name)
    {
        if (actual < required)
        {
            throw new ArgumentException($"The buffer requires at least {required} elements.",
                name);
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

    private static int CheckVUV(double* coarseAperiodicity, int numberOfAperiodicities,
        double* tmpAperiodicity)
    {
        double tmp = 0.0;
        for (int i = 0; i < numberOfAperiodicities; ++i)
        {
            tmp += coarseAperiodicity[i];
            tmpAperiodicity[i + 1] = coarseAperiodicity[i];
        }
        tmp /= numberOfAperiodicities;

        return tmp > -0.5 ? 1 : 0;
    }

    private static void GetAperiodicity(double* coarseFrequencyAxis, double* coarseAperiodicity,
        int numberOfAperiodicities, double* frequencyAxis, int fftSize, double* aperiodicity,
        in Interp1Scratch interpolation)
    {
        MatlabFunctions.Interp1(coarseFrequencyAxis, coarseAperiodicity,
            numberOfAperiodicities + 2, frequencyAxis, (fftSize / 2) + 1, aperiodicity,
            interpolation);
        for (int i = 0; i <= fftSize / 2; ++i)
        {
            aperiodicity[i] = Math.Pow(10.0, aperiodicity[i] / 20.0);
        }
    }

    private static double FrequencyToMel(double frequency)
    {
        return WorldConstants.MelM0 * Math.Log((frequency / WorldConstants.MelF0) + 1.0);
    }

    private static double MelToFrequency(double mel)
    {
        return WorldConstants.MelF0 * (Math.Exp(mel / WorldConstants.MelM0) - 1.0);
    }

    private static void DCTForCodec(double* melSpectrum, int maxDimension, FftComplex* weight,
        in ForwardRealFft forwardRealFft, int numberOfDimensions, double* melCepstrum)
    {
        int bias = maxDimension / 2;
        for (int i = 0; i < maxDimension / 2; ++i)
        {
            forwardRealFft.Waveform[i] = melSpectrum[i * 2];
            forwardRealFft.Waveform[i + bias] = melSpectrum[maxDimension - (i * 2) - 1];
        }
        forwardRealFft.ForwardFft.Execute();

        double normalization = Math.Sqrt(forwardRealFft.FftSize);
        for (int i = 0; i < numberOfDimensions; ++i)
        {
            melCepstrum[i] = ((forwardRealFft.Spectrum[i].Real * weight[i].Real) -
                (forwardRealFft.Spectrum[i].Imaginary * weight[i].Imaginary)) / normalization;
        }
    }

    private static void IDCTForCodec(double* melCepstrum, int maxDimension, FftComplex* weight,
        in InverseComplexFft inverseComplexFft, int numberOfDimensions, double* melSpectrum)
    {
        double normalization = Math.Sqrt(inverseComplexFft.FftSize);
        for (int i = 0; i < numberOfDimensions; ++i)
        {
            inverseComplexFft.Input[i].Real = melCepstrum[i] * weight[i].Real * normalization;
            inverseComplexFft.Input[i].Imaginary =
                -melCepstrum[i] * weight[i].Imaginary * normalization;
        }
        for (int i = numberOfDimensions; i < maxDimension; ++i)
        {
            inverseComplexFft.Input[i].Real = 0.0;
            inverseComplexFft.Input[i].Imaginary = 0.0;
        }

        inverseComplexFft.InverseFft.Execute();

        for (int i = 0; i < maxDimension / 2; ++i)
        {
            melSpectrum[i * 2] = inverseComplexFft.Output[i].Real;
            melSpectrum[(i * 2) + 1] = inverseComplexFft.Output[maxDimension - i - 1].Real;
        }
    }

    private static void CodeOneFrame(double* logSpectralEnvelope, double* frequencyAxis,
        int fftSize, double* melAxis, FftComplex* weight, int maxDimension,
        int numberOfDimensions, in ForwardRealFft forwardRealFft, double* codedSpectralEnvelope,
        double* melSpectrum, in Interp1Scratch interpolation)
    {
        MatlabFunctions.Interp1(frequencyAxis, logSpectralEnvelope, (fftSize / 2) + 1, melAxis,
            maxDimension, melSpectrum, interpolation);

        DCTForCodec(melSpectrum, maxDimension, weight, forwardRealFft, numberOfDimensions,
            codedSpectralEnvelope);
    }

    private static void DecodeOneFrame(double* codedSpectralEnvelope, double* frequencyAxis,
        int fftSize, double* melAxis, FftComplex* weight, int maxDimension,
        int numberOfDimensions, in InverseComplexFft inverseComplexFft, double* spectralEnvelope,
        double* melSpectrum, in Interp1Scratch interpolation)
    {
        IDCTForCodec(codedSpectralEnvelope, maxDimension, weight, inverseComplexFft,
            numberOfDimensions, melSpectrum + 1);
        melSpectrum[0] = melSpectrum[1];
        melSpectrum[maxDimension + 1] = melSpectrum[maxDimension];

        MatlabFunctions.Interp1(melAxis, melSpectrum, maxDimension + 2, frequencyAxis,
            (fftSize / 2) + 1, spectralEnvelope, interpolation);

        for (int i = 0; i < (fftSize / 2) + 1; ++i)
        {
            spectralEnvelope[i] = Math.Exp(spectralEnvelope[i] / maxDimension);
        }
    }

    private static void GetParametersForCoding(double floorFrequency, double ceilFrequency,
        int fs, int fftSize, double* melAxis, double* frequencyAxis, FftComplex* weight)
    {
        int maxDimension = fftSize / 2;
        double floorMel = FrequencyToMel(floorFrequency);
        double ceilMel = FrequencyToMel(ceilFrequency);

        for (int i = 0; i < maxDimension; ++i)
        {
            melAxis[i] = ((ceilMel - floorMel) * i / maxDimension) + floorMel;
            weight[i].Real = 2.0 * Math.Cos(i * WorldConstants.Pi / fftSize) / Math.Sqrt(fftSize);
            weight[i].Imaginary =
                2.0 * Math.Sin(i * WorldConstants.Pi / fftSize) / Math.Sqrt(fftSize);
        }
        weight[0].Real /= Math.Sqrt(2.0);

        for (int i = 0; i <= maxDimension; ++i)
        {
            frequencyAxis[i] = FrequencyToMel((double)i * fs / fftSize);
        }
    }

    private static void GetParametersForDecoding(double floorFrequency, double ceilFrequency,
        int fs, int fftSize, int numberOfDimensions, double* melAxis, double* frequencyAxis,
        FftComplex* weight)
    {
        int maxDimension = fftSize / 2;
        double floorMel = FrequencyToMel(floorFrequency);
        double ceilMel = FrequencyToMel(ceilFrequency);

        for (int i = 0; i < numberOfDimensions; ++i)
        {
            weight[i].Real = Math.Cos(i * WorldConstants.Pi / fftSize) * Math.Sqrt(fftSize);
            weight[i].Imaginary = Math.Sin(i * WorldConstants.Pi / fftSize) * Math.Sqrt(fftSize);
        }
        weight[0].Real /= Math.Sqrt(2.0);

        for (int i = 0; i < maxDimension; ++i)
        {
            melAxis[i + 1] =
                MelToFrequency(((ceilMel - floorMel) * i / maxDimension) + floorMel);
        }
        melAxis[0] = 0;
        melAxis[maxDimension + 1] = fs / 2.0;

        for (int i = 0; i < (fftSize / 2) + 1; ++i)
        {
            frequencyAxis[i] = (double)i * fs / fftSize;
        }
    }
}
