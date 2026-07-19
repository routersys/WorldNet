namespace WorldNet;

internal unsafe interface IScratchAllocator
{
    void* Allocate(int count, nuint elementSize);
}
