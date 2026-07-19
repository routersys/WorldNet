namespace WorldNet;

internal unsafe struct FastFftFiltScratch
{
    public FftComplex* XSpectrum;

    public static void Layout<TAllocator>(ref TAllocator allocator, int fftSize,
        ref FastFftFiltScratch scratch)
        where TAllocator : struct, IScratchAllocator
    {
        scratch.XSpectrum = (FftComplex*)allocator.Allocate(fftSize, (nuint)sizeof(FftComplex));
    }

    public static nuint GetRequiredArenaBytes(int fftSize)
    {
        MeasuringAllocator allocator = default;
        FastFftFiltScratch scratch = default;
        Layout(ref allocator, fftSize, ref scratch);
        return allocator.Total;
    }

    public static FastFftFiltScratch Bind(WorldArena arena, int fftSize)
    {
        ArenaAllocator allocator = new(arena);
        FastFftFiltScratch scratch = default;
        Layout(ref allocator, fftSize, ref scratch);
        return scratch;
    }
}
