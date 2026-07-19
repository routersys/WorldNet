namespace WorldNet;

[ScratchLayout]
internal unsafe partial struct CheapTrickScratch
{
    public ForwardRealFft ForwardRealFft;
    public InverseRealFft InverseRealFft;
    public double* SpectralEnvelope;
    public double* SmoothingLifter;
    public double* CompensationLifter;

    public static void Layout<TAllocator>(ref TAllocator allocator, int fftSize,
        ref CheapTrickScratch scratch)
        where TAllocator : struct, IScratchAllocator
    {
        ForwardRealFft.Layout(ref allocator, fftSize, ref scratch.ForwardRealFft);
        InverseRealFft.Layout(ref allocator, fftSize, ref scratch.InverseRealFft);
        scratch.SpectralEnvelope = (double*)allocator.Allocate(fftSize, sizeof(double));
        scratch.SmoothingLifter = (double*)allocator.Allocate(fftSize, sizeof(double));
        scratch.CompensationLifter = (double*)allocator.Allocate(fftSize, sizeof(double));
    }

    public static CheapTrickScratch Bind(WorldArena arena, int fftSize)
    {
        ArenaAllocator allocator = new(arena);
        CheapTrickScratch scratch = default;
        Layout(ref allocator, fftSize, ref scratch);
        scratch.ForwardRealFft.Initialize(fftSize);
        scratch.InverseRealFft.Initialize(fftSize);
        return scratch;
    }
}
