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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Negate(double* values, int count)
    {
        int i = 0;
        if (Vector.IsHardwareAccelerated && count >= Vector<double>.Count)
        {
            Vector<double> sign = new(-0.0);
            int limit = count - Vector<double>.Count;
            for (; i <= limit; i += Vector<double>.Count)
            {
                Unsafe.WriteUnaligned(values + i,
                    Vector.Xor(Unsafe.ReadUnaligned<Vector<double>>(values + i), sign));
            }
        }
        for (; i < count; ++i)
        {
            values[i] = -values[i];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SubtractNext(double* values, int count)
    {
        int i = 0;
        if (Vector.IsHardwareAccelerated && count >= Vector<double>.Count)
        {
            int limit = count - Vector<double>.Count;
            for (; i <= limit; i += Vector<double>.Count)
            {
                Unsafe.WriteUnaligned(values + i,
                    Unsafe.ReadUnaligned<Vector<double>>(values + i)
                    - Unsafe.ReadUnaligned<Vector<double>>(values + i + 1));
            }
        }
        for (; i < count; ++i)
        {
            values[i] -= values[i + 1];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Absolute(double* source, double* destination, int count)
    {
        int i = 0;
        if (Vector.IsHardwareAccelerated && count >= Vector<double>.Count)
        {
            int limit = count - Vector<double>.Count;
            for (; i <= limit; i += Vector<double>.Count)
            {
                Unsafe.WriteUnaligned(destination + i,
                    Vector.Abs(Unsafe.ReadUnaligned<Vector<double>>(source + i)));
            }
        }
        for (; i < count; ++i)
        {
            destination[i] = Math.Abs(source[i]);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void BlendAbsolute(double* first, double* second, double* destination,
        int count, double firstWeight, double secondWeight)
    {
        int i = 0;
        if (Vector.IsHardwareAccelerated && count >= Vector<double>.Count)
        {
            Vector<double> firstScale = new(firstWeight);
            Vector<double> secondScale = new(secondWeight);
            int limit = count - Vector<double>.Count;
            for (; i <= limit; i += Vector<double>.Count)
            {
                Unsafe.WriteUnaligned(destination + i,
                    (firstScale * Vector.Abs(Unsafe.ReadUnaligned<Vector<double>>(first + i)))
                    + (secondScale * Vector.Abs(Unsafe.ReadUnaligned<Vector<double>>(second + i))));
            }
        }
        for (; i < count; ++i)
        {
            destination[i] = (firstWeight * Math.Abs(first[i])) + (secondWeight * Math.Abs(second[i]));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector<double> ClampAperiodicity(Vector<double> value)
    {
        Vector<double> upper = new(0.999999999999);
        Vector<double> lower = new(0.001);
        Vector<double> limited = Vector.ConditionalSelect(Vector.LessThan(upper, value), upper, value);
        return Vector.ConditionalSelect(Vector.GreaterThan(lower, limited), lower, limited);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SquareAperiodicity(double* source, double* destination, int count)
    {
        int i = 0;
        if (Vector.IsHardwareAccelerated && count >= Vector<double>.Count)
        {
            int limit = count - Vector<double>.Count;
            for (; i <= limit; i += Vector<double>.Count)
            {
                Vector<double> safe = ClampAperiodicity(Unsafe.ReadUnaligned<Vector<double>>(source + i));
                Unsafe.WriteUnaligned(destination + i, safe * safe);
            }
        }
        for (; i < count; ++i)
        {
            double safe = WorldMath.GetSafeAperiodicity(source[i]);
            destination[i] = safe * safe;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void BlendSquareAperiodicity(double* first, double* second, double* destination,
        int count, double firstWeight, double secondWeight)
    {
        int i = 0;
        if (Vector.IsHardwareAccelerated && count >= Vector<double>.Count)
        {
            Vector<double> firstScale = new(firstWeight);
            Vector<double> secondScale = new(secondWeight);
            int limit = count - Vector<double>.Count;
            for (; i <= limit; i += Vector<double>.Count)
            {
                Vector<double> blended =
                    (firstScale * ClampAperiodicity(Unsafe.ReadUnaligned<Vector<double>>(first + i)))
                    + (secondScale * ClampAperiodicity(Unsafe.ReadUnaligned<Vector<double>>(second + i)));
                Unsafe.WriteUnaligned(destination + i, blended * blended);
            }
        }
        for (; i < count; ++i)
        {
            double blended = (firstWeight * WorldMath.GetSafeAperiodicity(first[i]))
                + (secondWeight * WorldMath.GetSafeAperiodicity(second[i]));
            destination[i] = blended * blended;
        }
    }
}
