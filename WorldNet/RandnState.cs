using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.X86;

namespace WorldNet;

[InlineArray(RandnConstants.Lanes * 4)]
internal struct RandnLaneStates
{
    private uint _element0;
}

[InlineArray(RandnConstants.BufferLength)]
internal struct RandnBuffer
{
    private uint _element0;
}

internal enum RandnPath
{
    Sequential,
    Narrow,
    Wide,
}

internal unsafe partial struct RandnState
{
    public uint X;
    public uint Y;
    public uint Z;
    public uint W;

    private RandnLaneStates _lanes;
    private RandnBuffer _buffer;
    private int _index;
    private RandnPath _path;

    public void Reseed()
    {
        Reseed(SelectPath());
    }

    private static RandnPath SelectPath()
    {
        return Avx2.IsSupported ? RandnPath.Wide : RandnPath.Sequential;
    }

    public void Reseed(RandnPath path)
    {
        X = 123456789;
        Y = 362436069;
        Z = 521288629;
        W = 88675123;
        _path = path;
        if (path != RandnPath.Sequential)
        {
            ReadOnlySpan<uint> initial = RandnConstants.InitialLanes;
            for (int lane = 0; lane < RandnConstants.Lanes; ++lane)
            {
                for (int word = 0; word < 4; ++word)
                {
                    _lanes[(word * RandnConstants.Lanes) + lane] = initial[(lane * 4) + word];
                }
            }
            _index = RandnConstants.BufferLength;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double Next()
    {
        if (_path != RandnPath.Sequential)
        {
            if (_index == RandnConstants.BufferLength)
            {
                RefillWide();
            }
            return (_buffer[_index++] / 268435456.0) - 6.0;
        }
        return NextSequential();
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private double NextSequential()
    {
        uint a = X;
        uint b = Y;
        uint c = Z;
        uint d = W;
        uint accumulator = 0;
        uint t;

        for (int i = 0; i < 3; ++i)
        {
            t = a ^ (a << 11);
            a = (d ^ (d >> 19)) ^ (t ^ (t >> 8));
            accumulator += a >> 4;

            t = b ^ (b << 11);
            b = (a ^ (a >> 19)) ^ (t ^ (t >> 8));
            accumulator += b >> 4;

            t = c ^ (c << 11);
            c = (b ^ (b >> 19)) ^ (t ^ (t >> 8));
            accumulator += c >> 4;

            t = d ^ (d << 11);
            d = (c ^ (c >> 19)) ^ (t ^ (t >> 8));
            accumulator += d >> 4;
        }

        X = a;
        Y = b;
        Z = c;
        W = d;

        return (accumulator / 268435456.0) - 6.0;
    }
}
