namespace WorldNet;

internal readonly unsafe struct ArenaAllocator : IScratchAllocator
{
    private readonly WorldArena _arena;

    public ArenaAllocator(WorldArena arena)
    {
        _arena = arena;
    }

    public void* Allocate(int count, nuint elementSize)
    {
        return _arena.AllocateRaw(count, elementSize);
    }
}
