namespace WorldNet;

public readonly ref struct WorldArenaScope
{
    private readonly WorldArena _arena;
    private readonly nuint _offset;

    internal WorldArenaScope(WorldArena arena, nuint offset)
    {
        _arena = arena;
        _offset = offset;
    }

    public void Dispose()
    {
        _arena.RestoreTo(_offset);
    }
}
