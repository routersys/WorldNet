namespace WorldNet;

internal static unsafe class Common
{
    public static int GetSuitableFftSize(int sample)
    {
        return (int)Math.Pow(2.0, (int)(Math.Log(sample) / WorldConstants.Log2) + 1.0);
    }

    public static int GetDcCorrectionUpperLimit(double f0, int fs, int fftSize)
    {
        return 2 + (int)(f0 * fftSize / fs);
    }

    public static void DcCorrection(double* input, double f0, int fs, int fftSize, double* output,
        in DcCorrectionScratch scratch)
    {
        int upperLimit = GetDcCorrectionUpperLimit(f0, fs, fftSize);
        double* lowFrequencyReplica = scratch.LowFrequencyReplica;
        double* lowFrequencyAxis = scratch.LowFrequencyAxis;

        double inverseFftSize = 1.0 / fftSize;
        for (int i = 0; i < upperLimit; ++i)
        {
            lowFrequencyAxis[i] = (double)i * fs * inverseFftSize;
        }

        int upperLimitReplica = upperLimit - 1;
        MatlabFunctions.Interp1Q(f0 - lowFrequencyAxis[0], -(double)fs * inverseFftSize, input,
            upperLimit + 1, lowFrequencyAxis, upperLimitReplica, lowFrequencyReplica,
            scratch.Interpolation);

        for (int i = 0; i < upperLimitReplica; ++i)
        {
            output[i] = input[i] + lowFrequencyReplica[i];
        }
    }

    public static int GetLinearSmoothingBoundary(double width, int fs, int fftSize)
    {
        return (int)(width * fftSize / fs) + 1;
    }

    public static void LinearSmoothing(double* input, double width, int fs, int fftSize,
        double* output, in LinearSmoothingScratch scratch)
    {
        int boundary = GetLinearSmoothingBoundary(width, fs, fftSize);

        SetParametersForLinearSmoothing(boundary, fftSize, fs, width, input,
            scratch.MirroringSpectrum, scratch.MirroringSegment, scratch.FrequencyAxis);

        double* lowLevels = scratch.LowLevels;
        double* highLevels = scratch.HighLevels;
        double originOfMirroringAxis = -(boundary - 0.5) * fs / fftSize;
        double discreteFrequencyInterval = (double)fs / fftSize;

        MatlabFunctions.Interp1Q(originOfMirroringAxis, discreteFrequencyInterval,
            scratch.MirroringSegment, (fftSize / 2) + (boundary * 2) + 1, scratch.FrequencyAxis,
            (fftSize / 2) + 1, lowLevels, scratch.Interpolation);

        for (int i = 0; i <= fftSize / 2; ++i)
        {
            scratch.FrequencyAxis[i] += width;
        }

        MatlabFunctions.Interp1Q(originOfMirroringAxis, discreteFrequencyInterval,
            scratch.MirroringSegment, (fftSize / 2) + (boundary * 2) + 1, scratch.FrequencyAxis,
            (fftSize / 2) + 1, highLevels, scratch.Interpolation);

        for (int i = 0; i <= fftSize / 2; ++i)
        {
            output[i] = (highLevels[i] - lowLevels[i]) / width;
        }
    }

    public static void NuttallWindow(int yLength, double* y)
    {
        for (int i = 0; i < yLength; ++i)
        {
            double tmp = i / (yLength - 1.0);
            y[i] = 0.355768 - (0.487396 * Math.Cos(2.0 * WorldConstants.Pi * tmp))
                + (0.144232 * Math.Cos(4.0 * WorldConstants.Pi * tmp))
                - (0.012604 * Math.Cos(6.0 * WorldConstants.Pi * tmp));
        }
    }

    private static void SetParametersForLinearSmoothing(int boundary, int fftSize, int fs,
        double width, double* powerSpectrum, double* mirroringSpectrum,
        double* mirroringSegment, double* frequencyAxis)
    {
        for (int i = 0; i < boundary; ++i)
        {
            mirroringSpectrum[i] = powerSpectrum[boundary - i];
        }
        for (int i = boundary; i < (fftSize / 2) + boundary; ++i)
        {
            mirroringSpectrum[i] = powerSpectrum[i - boundary];
        }
        for (int i = (fftSize / 2) + boundary; i <= (fftSize / 2) + (boundary * 2); ++i)
        {
            mirroringSpectrum[i] =
                powerSpectrum[(fftSize / 2) - (i - ((fftSize / 2) + boundary))];
        }

        double inverseFftSize = 1.0 / fftSize;
        mirroringSegment[0] = mirroringSpectrum[0] * fs * inverseFftSize;
        for (int i = 1; i < (fftSize / 2) + (boundary * 2) + 1; ++i)
        {
            mirroringSegment[i] =
                (mirroringSpectrum[i] * fs * inverseFftSize) + mirroringSegment[i - 1];
        }

        double halfWidth = width / 2.0;
        for (int i = 0; i <= fftSize / 2; ++i)
        {
            frequencyAxis[i] = ((double)i * inverseFftSize * fs) - halfWidth;
        }
    }
}
