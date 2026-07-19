namespace WorldNet;

[ScratchLayout]
internal unsafe partial struct FastFftFiltScratch
{
    public FftComplex* XSpectrum;

    public static void Layout<TAllocator>(ref TAllocator allocator, int fftSize,
        ref FastFftFiltScratch scratch)
        where TAllocator : struct, IScratchAllocator
    {
        scratch.XSpectrum = (FftComplex*)allocator.Allocate(fftSize, (nuint)sizeof(FftComplex));
    }
}
