using System.Numerics;

namespace WorldNet;

internal static class Validation
{
    public static void ThrowIfNotPowerOfTwo(int fftSize, string paramName)
    {
        if (!BitOperations.IsPow2(fftSize))
        {
            throw new ArgumentException(
                $"The FFT size must be a positive power of two but was {fftSize}.", paramName);
        }
    }
}
