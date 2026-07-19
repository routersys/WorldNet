namespace WorldNet.Tests;

public unsafe class MatlabFunctionsTests
{
    [Fact]
    public void MatlabRoundMatchesReference()
    {
        double[] input = ReferenceData.Load("mf_round_input").Values;
        double[] expected = ReferenceData.Load("mf_round_output").Values;

        for (int i = 0; i < input.Length; ++i)
        {
            Assert.Equal(expected[i], MatlabFunctions.MatlabRound(input[i]));
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    public void DecimateMatchesReference(int r)
    {
        double[] source = ReferenceData.Load("mf_decimate_input").Values;
        double[] expected = ReferenceData.Load($"mf_decimate_r{r}").Values;
        int length = source.Length;

        Assert.Equal(expected.Length, MatlabFunctions.GetDecimateOutputLength(length, r));

        using WorldArena arena = new(MatlabFunctions.GetDecimateArenaBytes(length) + 65536);
        double* x = (double*)arena.AllocateRaw(length, sizeof(double));
        double* y = (double*)arena.AllocateRaw(expected.Length, sizeof(double));
        for (int i = 0; i < length; ++i)
        {
            x[i] = source[i];
        }

        MatlabFunctions.Decimate(x, length, r, y, arena);

        AssertExact(expected, y, $"decimate r={r}");
    }

    [Fact]
    public void Interp1MatchesReference()
    {
        double[] sourceX = ReferenceData.Load("mf_interp1_x").Values;
        double[] sourceY = ReferenceData.Load("mf_interp1_y").Values;
        double[] sourceXi = ReferenceData.Load("mf_interp1_xi").Values;
        double[] expected = ReferenceData.Load("mf_interp1_yi").Values;

        using WorldArena arena = new(1 << 20);
        double* x = Copy(arena, sourceX);
        double* y = Copy(arena, sourceY);
        double* xi = Copy(arena, sourceXi);
        double* yi = (double*)arena.AllocateRaw(expected.Length, sizeof(double));

        MatlabFunctions.Interp1(x, y, sourceX.Length, xi, sourceXi.Length, yi, arena);

        AssertExact(expected, yi, "interp1");
    }

    [Fact]
    public void HistcMatchesReference()
    {
        double[] sourceX = ReferenceData.Load("mf_interp1_x").Values;
        double[] sourceXi = ReferenceData.Load("mf_interp1_xi").Values;
        double[] expected = ReferenceData.Load("mf_histc_index").Values;

        using WorldArena arena = new(1 << 20);
        double* x = Copy(arena, sourceX);
        double* edges = Copy(arena, sourceXi);
        int* index = (int*)arena.AllocateRaw(sourceXi.Length, sizeof(int));
        for (int i = 0; i < sourceXi.Length; ++i)
        {
            index[i] = 0;
        }

        MatlabFunctions.Histc(x, sourceX.Length, edges, sourceXi.Length, index);

        for (int i = 0; i < expected.Length; ++i)
        {
            Assert.Equal((int)expected[i], index[i]);
        }
    }

    [Fact]
    public void DiffMatchesReference()
    {
        double[] source = ReferenceData.Load("mf_interp1_y").Values;
        double[] expected = ReferenceData.Load("mf_diff_output").Values;

        using WorldArena arena = new(1 << 20);
        double* y = Copy(arena, source);
        double* output = (double*)arena.AllocateRaw(expected.Length, sizeof(double));

        MatlabFunctions.Diff(y, source.Length, output);

        AssertExact(expected, output, "diff");
    }

    [Fact]
    public void MatlabStdMatchesReference()
    {
        double[] source = ReferenceData.Load("mf_interp1_y").Values;
        double expected = ReferenceData.Load("mf_std_output").Values[0];

        using WorldArena arena = new(1 << 20);
        double* y = Copy(arena, source);

        Assert.Equal(expected, MatlabFunctions.MatlabStd(y, source.Length));
    }

    [Fact]
    public void FftShiftMatchesReference()
    {
        double[] source = ReferenceData.Load("mf_interp1_y").Values;
        double[] expected = ReferenceData.Load("mf_fftshift_output").Values;

        using WorldArena arena = new(1 << 20);
        double* y = Copy(arena, source);
        double* output = (double*)arena.AllocateRaw(expected.Length, sizeof(double));

        MatlabFunctions.FftShift(y, source.Length, output);

        AssertExact(expected, output, "fftshift");
    }

    [Fact]
    public void Interp1QMatchesReference()
    {
        double[] sourceY = ReferenceData.Load("mf_interp1_y").Values;
        double[] sourceXi = ReferenceData.Load("mf_interp1q_xi").Values;
        double[] expected = ReferenceData.Load("mf_interp1q_yi").Values;

        using WorldArena arena = new(1 << 20);
        double* y = Copy(arena, sourceY);
        double* xi = Copy(arena, sourceXi);
        double* yi = (double*)arena.AllocateRaw(expected.Length, sizeof(double));

        MatlabFunctions.Interp1Q(0.0, 1.5, y, sourceY.Length, xi, sourceXi.Length, yi, arena);

        AssertExact(expected, yi, "interp1Q");
    }

    [Fact]
    public void RandnMatchesReference()
    {
        double[] expected = ReferenceData.Load("mf_randn_values").Values;
        double[] expectedState = ReferenceData.Load("mf_randn_state").Values;

        RandnState state = default;
        state.Reseed();

        for (int i = 0; i < expected.Length; ++i)
        {
            Assert.Equal(expected[i], state.Next());
        }

        Assert.Equal((uint)expectedState[0], state.X);
        Assert.Equal((uint)expectedState[1], state.Y);
        Assert.Equal((uint)expectedState[2], state.Z);
        Assert.Equal((uint)expectedState[3], state.W);
    }

    private static double* Copy(WorldArena arena, double[] source)
    {
        double* target = (double*)arena.AllocateRaw(source.Length, sizeof(double));
        for (int i = 0; i < source.Length; ++i)
        {
            target[i] = source[i];
        }
        return target;
    }

    private static void AssertExact(double[] expected, double* actual, string label)
    {
        for (int i = 0; i < expected.Length; ++i)
        {
            if (!BitConverter.DoubleToInt64Bits(expected[i]).Equals(BitConverter.DoubleToInt64Bits(actual[i])))
            {
                Assert.Fail(
                    $"{label}: index {i} expected {expected[i]:E17} but was {actual[i]:E17}");
            }
        }
    }
}
