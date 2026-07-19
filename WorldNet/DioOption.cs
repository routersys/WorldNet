namespace WorldNet;

public readonly struct DioOption
{
    public double F0Floor { get; init; }
    public double F0Ceil { get; init; }
    public double ChannelsInOctave { get; init; }
    public double FramePeriod { get; init; }
    public int Speed { get; init; }
    public double AllowedRange { get; init; }

    public static DioOption Default => new()
    {
        ChannelsInOctave = 2.0,
        F0Ceil = WorldConstants.CeilF0,
        F0Floor = WorldConstants.FloorF0,
        FramePeriod = 5.0,
        Speed = 1,
        AllowedRange = 0.1,
    };
}
