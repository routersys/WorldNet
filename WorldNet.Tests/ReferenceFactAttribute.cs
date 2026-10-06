namespace WorldNet.Tests;

[AttributeUsage(AttributeTargets.Method)]
internal sealed class ReferenceFactAttribute : FactAttribute
{
    public ReferenceFactAttribute()
    {
        if (!ReferenceData.IsAvailable)
        {
            Skip = ReferenceData.MissingMessage;
        }
    }
}
