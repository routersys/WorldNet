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
    public int* NegativeGoingPoints;
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
        zeroCrossings.NegativeGoingPoints = (int*)allocator.Allocate(yLength, sizeof(int));
        zeroCrossings.Edges = (int*)allocator.Allocate(yLength, sizeof(int));
        zeroCrossings.FineEdges = (double*)allocator.Allocate(yLength, sizeof(double));
    }
}
