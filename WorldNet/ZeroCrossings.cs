using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace WorldNet;

[ScratchLayout]
internal unsafe partial struct ZeroCrossings
{
    public double* NegativeIntervalLocations;
    public double* NegativeIntervals;
    public int NumberOfNegatives;
    public double* PositiveIntervalLocations;
    public double* PositiveIntervals;
    public int NumberOfPositives;
    public double* PeakIntervalLocations;
    public double* PeakIntervals;
    public int NumberOfPeaks;
    public double* DipIntervalLocations;
    public double* DipIntervals;
    public int NumberOfDips;
    public int* Edges;
    public double* FineEdges;

    public static void Layout<TAllocator>(ref TAllocator allocator, int yLength,
        ref ZeroCrossings zeroCrossings)
        where TAllocator : struct, IScratchAllocator
    {
        zeroCrossings.NegativeIntervalLocations = (double*)allocator.Allocate(yLength, sizeof(double));
        zeroCrossings.PositiveIntervalLocations = (double*)allocator.Allocate(yLength, sizeof(double));
        zeroCrossings.PeakIntervalLocations = (double*)allocator.Allocate(yLength, sizeof(double));
        zeroCrossings.DipIntervalLocations = (double*)allocator.Allocate(yLength, sizeof(double));
        zeroCrossings.NegativeIntervals = (double*)allocator.Allocate(yLength, sizeof(double));
        zeroCrossings.PositiveIntervals = (double*)allocator.Allocate(yLength, sizeof(double));
        zeroCrossings.PeakIntervals = (double*)allocator.Allocate(yLength, sizeof(double));
        zeroCrossings.DipIntervals = (double*)allocator.Allocate(yLength, sizeof(double));
        zeroCrossings.Edges = (int*)allocator.Allocate(yLength, sizeof(int));
        zeroCrossings.FineEdges = (double*)allocator.Allocate(yLength, sizeof(double));
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void Find(double* signal, int length, double fs, ref ZeroCrossings zeroCrossings)
    {
        zeroCrossings.NumberOfNegatives = Engine(signal, length, fs,
            zeroCrossings.NegativeIntervalLocations, zeroCrossings.NegativeIntervals,
            zeroCrossings.Edges, zeroCrossings.FineEdges);

        VectorOperations.Negate(signal, length);
        zeroCrossings.NumberOfPositives = Engine(signal, length, fs,
            zeroCrossings.PositiveIntervalLocations, zeroCrossings.PositiveIntervals,
            zeroCrossings.Edges, zeroCrossings.FineEdges);

        VectorOperations.SubtractNext(signal, length - 1);
        zeroCrossings.NumberOfPeaks = Engine(signal, length - 1, fs,
            zeroCrossings.PeakIntervalLocations, zeroCrossings.PeakIntervals,
            zeroCrossings.Edges, zeroCrossings.FineEdges);

        VectorOperations.Negate(signal, length - 1);
        zeroCrossings.NumberOfDips = Engine(signal, length - 1, fs,
            zeroCrossings.DipIntervalLocations, zeroCrossings.DipIntervals,
            zeroCrossings.Edges, zeroCrossings.FineEdges);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static int Engine(double* signal, int length, double fs, double* intervalLocations,
        double* intervals, int* edges, double* fineEdges)
    {
        if (length < 2)
        {
            return 0;
        }

        int count = FindNegativeGoingPoints(signal, length, edges);
        if (count < 2)
        {
            return 0;
        }

        for (int i = 0; i < count; ++i)
        {
            fineEdges[i] = edges[i] - (signal[edges[i] - 1] /
                (signal[edges[i]] - signal[edges[i] - 1]));
        }

        for (int i = 0; i < count - 1; ++i)
        {
            intervals[i] = fs / (fineEdges[i + 1] - fineEdges[i]);
            intervalLocations[i] = (fineEdges[i] + fineEdges[i + 1]) / 2.0 / fs;
        }
        return count - 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static int FindNegativeGoingPoints(double* signal, int length, int* edges)
    {
        int pairs = length - 1;
        int count = 0;
        int i = 0;

        if (Vector256.IsHardwareAccelerated)
        {
            for (; i + Vector256<double>.Count <= pairs; i += Vector256<double>.Count)
            {
                uint mask = (Vector256.GreaterThan(Vector256.Load(signal + i),
                        Vector256<double>.Zero)
                    & Vector256.LessThanOrEqual(Vector256.Load(signal + i + 1),
                        Vector256<double>.Zero)).ExtractMostSignificantBits();
                while (mask != 0)
                {
                    edges[count++] = i + BitOperations.TrailingZeroCount(mask) + 1;
                    mask &= mask - 1;
                }
            }
        }
        else if (Vector128.IsHardwareAccelerated)
        {
            for (; i + Vector128<double>.Count <= pairs; i += Vector128<double>.Count)
            {
                uint mask = (Vector128.GreaterThan(Vector128.Load(signal + i),
                        Vector128<double>.Zero)
                    & Vector128.LessThanOrEqual(Vector128.Load(signal + i + 1),
                        Vector128<double>.Zero)).ExtractMostSignificantBits();
                while (mask != 0)
                {
                    edges[count++] = i + BitOperations.TrailingZeroCount(mask) + 1;
                    mask &= mask - 1;
                }
            }
        }

        for (; i < pairs; ++i)
        {
            edges[count] = i + 1;
            count += 0.0 < signal[i] && signal[i + 1] <= 0.0 ? 1 : 0;
        }
        return count;
    }
}
