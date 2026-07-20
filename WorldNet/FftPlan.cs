using System.Numerics;
using System.Runtime.CompilerServices;

namespace WorldNet;

internal enum FftDirection
{
    Forward = 1,
    Backward = 2,
}

internal enum FftPlanKind
{
    RealToComplex,
    ComplexToReal,
    ComplexToComplex,
}

internal unsafe struct FftPlan
{
    public int N;
    public FftDirection Sign;
    public double* Input;
    public int* Ip;
    public double* W;
    public double* In;
    public double* Out;
    public FftComplex* CIn;
    public FftComplex* COut;

    public static void Layout<TAllocator>(ref TAllocator allocator, int n, FftPlanKind kind,
        ref FftPlan plan)
        where TAllocator : struct, IScratchAllocator
    {
        int inputCount = kind == FftPlanKind.ComplexToComplex ? n * 2 : n;
        plan.Input = (double*)allocator.Allocate(inputCount, sizeof(double));
        plan.Ip = (int*)allocator.Allocate(n, sizeof(int));
        plan.W = (double*)allocator.Allocate(n * 5 / 4, sizeof(double));
    }

    public static nuint GetRequiredArenaBytes(int n, FftPlanKind kind)
    {
        MeasuringAllocator allocator = default;
        FftPlan plan = default;
        Layout(ref allocator, n, kind, ref plan);
        return allocator.Total;
    }

    public static FftPlan BindRealToComplex(WorldArena arena, int n, double* input,
        FftComplex* output)
    {
        ArenaAllocator allocator = new(arena);
        FftPlan plan = default;
        Layout(ref allocator, n, FftPlanKind.RealToComplex, ref plan);
        plan.InitializeRealToComplex(n, input, output);
        return plan;
    }

    public static FftPlan BindComplexToReal(WorldArena arena, int n, FftComplex* input,
        double* output)
    {
        ArenaAllocator allocator = new(arena);
        FftPlan plan = default;
        Layout(ref allocator, n, FftPlanKind.ComplexToReal, ref plan);
        plan.InitializeComplexToReal(n, input, output);
        return plan;
    }

    public static FftPlan BindComplexToComplex(WorldArena arena, int n, FftComplex* input,
        FftComplex* output, FftDirection sign)
    {
        ArenaAllocator allocator = new(arena);
        FftPlan plan = default;
        Layout(ref allocator, n, FftPlanKind.ComplexToComplex, ref plan);
        plan.InitializeComplexToComplex(n, input, output, sign);
        return plan;
    }

    public void InitializeRealToComplex(int n, double* input, FftComplex* output)
    {
        N = n;
        Sign = FftDirection.Forward;
        In = input;
        Out = null;
        CIn = null;
        COut = output;
        Ip[0] = 0;
        OouraFft.MakeWt(N >> 2, Ip, W);
        OouraFft.MakeCt(N >> 2, Ip, W + (N >> 2));
    }

    public void InitializeComplexToReal(int n, FftComplex* input, double* output)
    {
        N = n;
        Sign = FftDirection.Backward;
        In = null;
        Out = output;
        CIn = input;
        COut = null;
        Ip[0] = 0;
        OouraFft.MakeWt(N >> 2, Ip, W);
        OouraFft.MakeCt(N >> 2, Ip, W + (N >> 2));
    }

    public void InitializeComplexToComplex(int n, FftComplex* input, FftComplex* output,
        FftDirection sign)
    {
        N = n;
        Sign = sign;
        In = null;
        Out = null;
        CIn = input;
        COut = output;
        Ip[0] = 0;
        OouraFft.MakeWt(N >> 1, Ip, W);
    }

    public readonly void Execute()
    {
        if (Sign == FftDirection.Forward)
        {
            ExecuteForward();
        }
        else
        {
            ExecuteBackward();
        }
    }

    private readonly void ExecuteForward()
    {
        if (CIn is null)
        {
            Copy(In, Input, N);
            OouraFft.Rdft(N, 1, Input, Ip, W);
            COut[0].Real = Input[0];
            COut[0].Imaginary = 0.0;
            CopyConjugate(Input + 2, (double*)(COut + 1), N - 2);
            COut[N / 2].Real = Input[1];
            COut[N / 2].Imaginary = 0.0;
        }
        else
        {
            Copy((double*)CIn, Input, N * 2);
            OouraFft.Cdft(N * 2, 1, Input, Ip, W);
            CopyConjugate(Input, (double*)COut, N * 2);
        }
    }

    private readonly void ExecuteBackward()
    {
        if (COut is null)
        {
            double nyquist = CIn[N / 2].Real;
            CopyConjugate((double*)CIn, Input, N);
            Input[1] = nyquist;
            OouraFft.Rdft(N, -1, Input, Ip, W);
            Scale(Input, Out, N, 2.0);
        }
        else
        {
            Copy((double*)CIn, Input, N * 2);
            OouraFft.Cdft(N * 2, -1, Input, Ip, W);
            CopyConjugate(Input, (double*)COut, N * 2);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Copy(double* source, double* destination, int count)
    {
        Buffer.MemoryCopy(source, destination, (long)count * sizeof(double),
            (long)count * sizeof(double));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CopyConjugate(double* source, double* destination, int count)
    {
        int i = 0;
        if (Vector.IsHardwareAccelerated && count >= Vector<double>.Count)
        {
            Vector<double> sign = AlternatingSign;
            int limit = count - Vector<double>.Count;
            for (; i <= limit; i += Vector<double>.Count)
            {
                Unsafe.WriteUnaligned(destination + i,
                    Unsafe.ReadUnaligned<Vector<double>>(source + i) * sign);
            }
        }
        for (; i < count; ++i)
        {
            destination[i] = (i & 1) == 0 ? source[i] : -source[i];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Scale(double* source, double* destination, int count, double factor)
    {
        int i = 0;
        if (Vector.IsHardwareAccelerated && count >= Vector<double>.Count)
        {
            Vector<double> scale = new(factor);
            int limit = count - Vector<double>.Count;
            for (; i <= limit; i += Vector<double>.Count)
            {
                Unsafe.WriteUnaligned(destination + i,
                    Unsafe.ReadUnaligned<Vector<double>>(source + i) * scale);
            }
        }
        for (; i < count; ++i)
        {
            destination[i] = source[i] * factor;
        }
    }

    private static readonly Vector<double> AlternatingSign = CreateAlternatingSign();

    private static Vector<double> CreateAlternatingSign()
    {
        Span<double> values = stackalloc double[Vector<double>.Count];
        for (int i = 0; i < values.Length; ++i)
        {
            values[i] = (i & 1) == 0 ? 1.0 : -1.0;
        }
        return new Vector<double>(values);
    }
}
