namespace WorldNet;

public readonly struct HarvestOption
{
    public double F0Floor { get; init; }
    public double F0Ceil { get; init; }
    public double FramePeriod { get; init; }

    public static HarvestOption Default => new()
    {
        F0Ceil = WorldConstants.CeilF0,
        F0Floor = WorldConstants.FloorF0,
        FramePeriod = 5.0,
    };
}
