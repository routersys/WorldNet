namespace WorldNet;

internal unsafe struct MeasuringAllocator : IScratchAllocator
{
    public nuint Total;

    public void* Allocate(int count, nuint elementSize)
    {
        Total += WorldArena.GetReservedBytes(count, elementSize);
        return null;
    }
}
