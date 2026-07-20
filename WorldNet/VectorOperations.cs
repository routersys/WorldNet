using System.Numerics;
using System.Runtime.CompilerServices;

namespace WorldNet;

internal static unsafe class VectorOperations
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void DivideByScalar(double* values, int count, double divisor)
    {
        int i = 0;
        if (Vector.IsHardwareAccelerated && count >= Vector<double>.Count)
        {
            Vector<double> denominator = new(divisor);
            int limit = count - Vector<double>.Count;
            for (; i <= limit; i += Vector<double>.Count)
            {
                Unsafe.WriteUnaligned(values + i,
                    Unsafe.ReadUnaligned<Vector<double>>(values + i) / denominator);
            }
        }
        for (; i < count; ++i)
        {
            values[i] /= divisor;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SubtractAndDivideByScalar(double* left, double* right, double* destination,
        int count, double divisor)
    {
        int i = 0;
        if (Vector.IsHardwareAccelerated && count >= Vector<double>.Count)
        {
            Vector<double> denominator = new(divisor);
            int limit = count - Vector<double>.Count;
            for (; i <= limit; i += Vector<double>.Count)
            {
                Vector<double> difference = Unsafe.ReadUnaligned<Vector<double>>(left + i)
                    - Unsafe.ReadUnaligned<Vector<double>>(right + i);
                Unsafe.WriteUnaligned(destination + i, difference / denominator);
            }
        }
        for (; i < count; ++i)
        {
            destination[i] = (left[i] - right[i]) / divisor;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SubtractScaled(double* target, double* source, int count, double factor)
    {
        int i = 0;
        if (Vector.IsHardwareAccelerated && count >= Vector<double>.Count)
        {
            Vector<double> scale = new(factor);
            int limit = count - Vector<double>.Count;
            for (; i <= limit; i += Vector<double>.Count)
            {
                Vector<double> scaled =
                    Unsafe.ReadUnaligned<Vector<double>>(source + i) * scale;
                Unsafe.WriteUnaligned(target + i,
                    Unsafe.ReadUnaligned<Vector<double>>(target + i) - scaled);
            }
        }
        for (; i < count; ++i)
        {
            target[i] -= source[i] * factor;
        }
    }
}
