namespace WorldNet;

public readonly unsafe ref struct WorldArenaScope
{
    private readonly WorldArena _arena;
    private readonly void* _chunk;
    private readonly nuint _used;

    internal WorldArenaScope(WorldArena arena, void* chunk, nuint used)
    {
        _arena = arena;
        _chunk = chunk;
        _used = used;
    }

    public void Dispose()
    {
        _arena.RestoreTo(_chunk, _used);
    }
}
