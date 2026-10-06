namespace WorldNet.Tests;

public unsafe class ZeroCrossingsTests
{
    private const double SamplingFrequency = 7350.0;

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    [InlineData(3, 1)]
    [InlineData(4, 0)]
    [InlineData(5, 2)]
    [InlineData(7, 1)]
    [InlineData(8, 2)]
    [InlineData(9, 0)]
    [InlineData(15, 1)]
    [InlineData(16, 2)]
    [InlineData(17, 0)]
    [InlineData(31, 1)]
    [InlineData(100, 0)]
    [InlineData(100, 1)]
    [InlineData(100, 2)]
    [InlineData(1000, 0)]
    [InlineData(1000, 1)]
    [InlineData(1000, 2)]
    [InlineData(5834, 0)]
    [InlineData(5834, 1)]
    [InlineData(5834, 2)]
    public void FindMatchesScalarReference(int length, int kind)
    {
        double[] signal = CreateSignal(length, kind);
        double[] expectedSignal = (double[])signal.Clone();
        Reference expected = Reference.Find(expectedSignal, length);

        using WorldArena arena = new();
        ZeroCrossings zeroCrossings = ZeroCrossings.Bind(arena, Math.Max(length, 1));
        fixed (double* signalPointer = signal)
        {
            ZeroCrossings.Find(signalPointer, length, SamplingFrequency, ref zeroCrossings);
        }

        AssertSet(expected.NegativeCount, expected.NegativeIntervals,
            expected.NegativeLocations, zeroCrossings.NumberOfNegatives,
            zeroCrossings.NegativeIntervals, zeroCrossings.NegativeIntervalLocations, "negative");
        AssertSet(expected.PositiveCount, expected.PositiveIntervals,
            expected.PositiveLocations, zeroCrossings.NumberOfPositives,
            zeroCrossings.PositiveIntervals, zeroCrossings.PositiveIntervalLocations, "positive");
        AssertSet(expected.PeakCount, expected.PeakIntervals, expected.PeakLocations,
            zeroCrossings.NumberOfPeaks, zeroCrossings.PeakIntervals,
            zeroCrossings.PeakIntervalLocations, "peak");
        AssertSet(expected.DipCount, expected.DipIntervals, expected.DipLocations,
            zeroCrossings.NumberOfDips, zeroCrossings.DipIntervals,
            zeroCrossings.DipIntervalLocations, "dip");
    }

    private static void AssertSet(int expectedCount, double[] expectedIntervals,
        double[] expectedLocations, int actualCount, double* actualIntervals,
        double* actualLocations, string name)
    {
        Assert.Equal(expectedCount, actualCount);
        for (int i = 0; i < expectedCount; ++i)
        {
            Assert.Equal(BitConverter.DoubleToInt64Bits(expectedIntervals[i]),
                BitConverter.DoubleToInt64Bits(actualIntervals[i]));
            Assert.Equal(BitConverter.DoubleToInt64Bits(expectedLocations[i]),
                BitConverter.DoubleToInt64Bits(actualLocations[i]));
        }
    }

    private static double[] CreateSignal(int length, int kind)
    {
        Random random = new((length * 31) + kind);
        double[] signal = new double[length];
        for (int i = 0; i < length; ++i)
        {
            double value = (random.NextDouble() * 2.0) - 1.0;
            if (kind == 1)
            {
                value = Math.Round(value * 2.0) / 2.0;
            }
            else if (kind == 2)
            {
                int selector = random.Next(12);
                value = selector switch
                {
                    0 => 0.0,
                    1 => -0.0,
                    2 => double.NaN,
                    _ => value,
                };
            }
            signal[i] = value;
        }
        return signal;
    }

    private sealed class Reference
    {
        public int NegativeCount;
        public int PositiveCount;
        public int PeakCount;
        public int DipCount;
        public double[] NegativeIntervals = [];
        public double[] NegativeLocations = [];
        public double[] PositiveIntervals = [];
        public double[] PositiveLocations = [];
        public double[] PeakIntervals = [];
        public double[] PeakLocations = [];
        public double[] DipIntervals = [];
        public double[] DipLocations = [];

        public static Reference Find(double[] signal, int length)
        {
            Reference result = new();
            result.NegativeCount = Engine(signal, length, out result.NegativeIntervals,
                out result.NegativeLocations);

            for (int i = 0; i < length; ++i)
            {
                signal[i] = -signal[i];
            }
            result.PositiveCount = Engine(signal, length, out result.PositiveIntervals,
                out result.PositiveLocations);

            for (int i = 0; i < length - 1; ++i)
            {
                signal[i] = signal[i] - signal[i + 1];
            }
            result.PeakCount = Engine(signal, length - 1, out result.PeakIntervals,
                out result.PeakLocations);

            for (int i = 0; i < length - 1; ++i)
            {
                signal[i] = -signal[i];
            }
            result.DipCount = Engine(signal, length - 1, out result.DipIntervals,
                out result.DipLocations);
            return result;
        }

        private static int Engine(double[] signal, int length, out double[] intervals,
            out double[] locations)
        {
            intervals = new double[Math.Max(length, 1)];
            locations = new double[Math.Max(length, 1)];
            if (length < 2)
            {
                return 0;
            }

            int[] negativeGoingPoints = new int[length];
            for (int i = 0; i < length - 1; ++i)
            {
                negativeGoingPoints[i] = 0.0 < signal[i] && signal[i + 1] <= 0.0 ? i + 1 : 0;
            }
            negativeGoingPoints[length - 1] = 0;

            int[] edges = new int[length];
            int count = 0;
            for (int i = 0; i < length; ++i)
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

            double[] fineEdges = new double[length];
            for (int i = 0; i < count; ++i)
            {
                fineEdges[i] = edges[i] - (signal[edges[i] - 1] /
                    (signal[edges[i]] - signal[edges[i] - 1]));
            }

            for (int i = 0; i < count - 1; ++i)
            {
                intervals[i] = SamplingFrequency / (fineEdges[i + 1] - fineEdges[i]);
                locations[i] = (fineEdges[i] + fineEdges[i + 1]) / 2.0 / SamplingFrequency;
            }
            return count - 1;
        }
    }
}
