namespace WorldNet;

public readonly struct CheapTrickOption
{
    public double Q1 { get; init; }
    public double F0Floor { get; init; }
    public int FftSize { get; init; }

    public static CheapTrickOption Create(int fs)
    {
        double f0Floor = WorldConstants.FloorF0;
        return new()
        {
            Q1 = -0.15,
            F0Floor = f0Floor,
            FftSize = CheapTrick.GetFftSize(fs, f0Floor),
        };
    }
}
