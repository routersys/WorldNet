using System.Runtime.Intrinsics.X86;

namespace WorldNet.Tests;

public class RandnStateTests
{
    private const int StepsPerSample = 12;

    [Fact]
    public void WideStreamMatchesSequentialStream()
    {
        if (!Avx2.IsSupported)
        {
            return;
        }

        RandnState sequential = default;
        sequential.Reseed(false);
        RandnState wide = default;
        wide.Reseed(true);

        for (int i = 0; i < RandnConstants.BufferLength * 300; ++i)
        {
            Assert.Equal(
                BitConverter.DoubleToInt64Bits(sequential.Next()),
                BitConverter.DoubleToInt64Bits(wide.Next()));
        }
    }

    [Fact]
    public void WideStreamRestartsFromTheSeedAfterReseed()
    {
        if (!Avx2.IsSupported)
        {
            return;
        }

        RandnState wide = default;
        wide.Reseed(true);
        double[] first = new double[RandnConstants.BufferLength + 100];
        for (int i = 0; i < first.Length; ++i)
        {
            first[i] = wide.Next();
        }

        wide.Reseed(true);
        for (int i = 0; i < first.Length; ++i)
        {
            Assert.Equal(BitConverter.DoubleToInt64Bits(first[i]),
                BitConverter.DoubleToInt64Bits(wide.Next()));
        }
    }

    [Fact]
    public void InitialLanesAreTheSequentialStateAtEachChunkStart()
    {
        ReadOnlySpan<uint> initial = RandnConstants.InitialLanes;
        (uint x, uint y, uint z, uint w) state = (123456789, 362436069, 521288629, 88675123);
        for (int lane = 0; lane < RandnConstants.Lanes; ++lane)
        {
            Assert.Equal(initial[lane * 4], state.x);
            Assert.Equal(initial[(lane * 4) + 1], state.y);
            Assert.Equal(initial[(lane * 4) + 2], state.z);
            Assert.Equal(initial[(lane * 4) + 3], state.w);
            for (int i = 0; i < RandnConstants.ChunkLength * StepsPerSample; ++i)
            {
                state = Step(state);
            }
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void JumpColumnsAdvanceTheStateByTheDistanceBetweenChunks(int seed)
    {
        Random random = new(seed);
        (uint x, uint y, uint z, uint w) state = (
            (uint)random.NextInt64(), (uint)random.NextInt64(),
            (uint)random.NextInt64(), (uint)random.NextInt64());

        ReadOnlySpan<uint> columns = RandnConstants.JumpColumns;
        uint[] jumped = new uint[4];
        uint[] words = [state.x, state.y, state.z, state.w];
        for (int bit = 0; bit < 128; ++bit)
        {
            if (((words[bit / 32] >> (bit % 32)) & 1) == 0)
            {
                continue;
            }
            for (int k = 0; k < 4; ++k)
            {
                jumped[k] ^= columns[(bit * 4) + k];
            }
        }

        int distance = (RandnConstants.Lanes - 1) * RandnConstants.ChunkLength * StepsPerSample;
        for (int i = 0; i < distance; ++i)
        {
            state = Step(state);
        }

        Assert.Equal([state.x, state.y, state.z, state.w], jumped);
    }

    private static (uint x, uint y, uint z, uint w) Step((uint x, uint y, uint z, uint w) state)
    {
        uint t = state.x ^ (state.x << 11);
        uint next = (state.w ^ (state.w >> 19)) ^ (t ^ (t >> 8));
        return (state.y, state.z, state.w, next);
    }
}
