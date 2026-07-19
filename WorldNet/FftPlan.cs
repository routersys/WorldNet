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
            for (int i = 0; i < N; ++i)
            {
                Input[i] = In[i];
            }
            OouraFft.Rdft(N, 1, Input, Ip, W);
            COut[0].Real = Input[0];
            COut[0].Imaginary = 0.0;
            for (int i = 1; i < N / 2; ++i)
            {
                COut[i].Real = Input[i * 2];
                COut[i].Imaginary = -Input[i * 2 + 1];
            }
            COut[N / 2].Real = Input[1];
            COut[N / 2].Imaginary = 0.0;
        }
        else
        {
            for (int i = 0; i < N; ++i)
            {
                Input[i * 2] = CIn[i].Real;
                Input[i * 2 + 1] = CIn[i].Imaginary;
            }
            OouraFft.Cdft(N * 2, 1, Input, Ip, W);
            for (int i = 0; i < N; ++i)
            {
                COut[i].Real = Input[i * 2];
                COut[i].Imaginary = -Input[i * 2 + 1];
            }
        }
    }

    private readonly void ExecuteBackward()
    {
        if (COut is null)
        {
            Input[0] = CIn[0].Real;
            Input[1] = CIn[N / 2].Real;
            for (int i = 1; i < N / 2; ++i)
            {
                Input[i * 2] = CIn[i].Real;
                Input[i * 2 + 1] = -CIn[i].Imaginary;
            }
            OouraFft.Rdft(N, -1, Input, Ip, W);
            for (int i = 0; i < N; ++i)
            {
                Out[i] = Input[i] * 2.0;
            }
        }
        else
        {
            for (int i = 0; i < N; ++i)
            {
                Input[i * 2] = CIn[i].Real;
                Input[i * 2 + 1] = CIn[i].Imaginary;
            }
            OouraFft.Cdft(N * 2, -1, Input, Ip, W);
            for (int i = 0; i < N; ++i)
            {
                COut[i].Real = Input[i * 2];
                COut[i].Imaginary = -Input[i * 2 + 1];
            }
        }
    }
}
