namespace WorldNet.Tests;

internal static class WaveformComparison
{
    public const double PeakUlpTolerance = 4.0;

    public static double MaxErrorInPeakUlps(ReadOnlySpan<double> expected, ReadOnlySpan<double> actual)
    {
        double peak = 0.0;
        double maxError = 0.0;
        for (int i = 0; i < expected.Length; ++i)
        {
            peak = Math.Max(peak, Math.Abs(expected[i]));
            maxError = Math.Max(maxError, Math.Abs(expected[i] - actual[i]));
        }

        return maxError / (Math.BitIncrement(peak) - peak);
    }
}
