namespace WorldNet.Tests;

[AttributeUsage(AttributeTargets.Method)]
internal sealed class ReferenceTheoryAttribute : TheoryAttribute
{
    public ReferenceTheoryAttribute()
    {
        if (!ReferenceData.IsAvailable)
        {
            Skip = ReferenceData.MissingMessage;
        }
    }
}
