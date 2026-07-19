namespace WorldNet;

public readonly struct D4COption
{
    public double Threshold { get; init; }

    public static D4COption Default => new()
    {
        Threshold = WorldConstants.Threshold,
    };
}
