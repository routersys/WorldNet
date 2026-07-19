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

    public static nuint GetRequiredArenaBytes(int n, FftPlanKind kind)
    {
        int inputCount = kind == FftPlanKind.ComplexToComplex ? n * 2 : n;
        return WorldArena.GetReservedBytes(inputCount, sizeof(double))
            + WorldArena.GetReservedBytes(n, sizeof(int))
            + WorldArena.GetReservedBytes(n * 5 / 4, sizeof(double));
    }

    public static FftPlan CreateRealToComplex(int n, double* input, FftComplex* output, WorldArena arena)
    {
        FftPlan plan = default;
        plan.N = n;
        plan.In = input;
        plan.COut = output;
        plan.Sign = FftDirection.Forward;
        plan.AllocateTables(n, n, arena);
        OouraFft.MakeWt(plan.N >> 2, plan.Ip, plan.W);
        OouraFft.MakeCt(plan.N >> 2, plan.Ip, plan.W + (plan.N >> 2));
        return plan;
    }

    public static FftPlan CreateComplexToReal(int n, FftComplex* input, double* output, WorldArena arena)
    {
        FftPlan plan = default;
        plan.N = n;
        plan.CIn = input;
        plan.Out = output;
        plan.Sign = FftDirection.Backward;
        plan.AllocateTables(n, n, arena);
        OouraFft.MakeWt(plan.N >> 2, plan.Ip, plan.W);
        OouraFft.MakeCt(plan.N >> 2, plan.Ip, plan.W + (plan.N >> 2));
        return plan;
    }

    public static FftPlan CreateComplexToComplex(int n, FftComplex* input, FftComplex* output,
        FftDirection sign, WorldArena arena)
    {
        FftPlan plan = default;
        plan.N = n;
        plan.CIn = input;
        plan.COut = output;
        plan.Sign = sign;
        plan.AllocateTables(n * 2, n, arena);
        OouraFft.MakeWt(plan.N >> 1, plan.Ip, plan.W);
        return plan;
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

    private void AllocateTables(int inputCount, int n, WorldArena arena)
    {
        Input = (double*)arena.AllocateRaw(inputCount, sizeof(double));
        Ip = (int*)arena.AllocateRaw(n, sizeof(int));
        W = (double*)arena.AllocateRaw(n * 5 / 4, sizeof(double));
        Ip[0] = 0;
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
