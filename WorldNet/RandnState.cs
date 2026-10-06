using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
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

internal unsafe struct RandnState
{
    public uint X;
    public uint Y;
    public uint Z;
    public uint W;

    private RandnLaneStates _lanes;
    private RandnBuffer _buffer;
    private int _index;
    private bool _wide;

    public void Reseed()
    {
        Reseed(Avx2.IsSupported);
    }

    public void Reseed(bool wide)
    {
        X = 123456789;
        Y = 362436069;
        Z = 521288629;
        W = 88675123;
        _wide = wide;
        if (wide)
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
        if (_wide)
        {
            if (_index == RandnConstants.BufferLength)
            {
                Refill();
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

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void Refill()
    {
        fixed (uint* lanes = &_lanes[0])
        fixed (uint* buffer = &_buffer[0])
        {
            Vector256<uint> a = Vector256.Load(lanes);
            Vector256<uint> b = Vector256.Load(lanes + RandnConstants.Lanes);
            Vector256<uint> c = Vector256.Load(lanes + (2 * RandnConstants.Lanes));
            Vector256<uint> d = Vector256.Load(lanes + (3 * RandnConstants.Lanes));
            for (int n = 0; n < RandnConstants.ChunkLength; n += 8)
            {
                Vector256<uint> r0 = NextLanes(ref a, ref b, ref c, ref d);
                Vector256<uint> r1 = NextLanes(ref a, ref b, ref c, ref d);
                Vector256<uint> r2 = NextLanes(ref a, ref b, ref c, ref d);
                Vector256<uint> r3 = NextLanes(ref a, ref b, ref c, ref d);
                Vector256<uint> r4 = NextLanes(ref a, ref b, ref c, ref d);
                Vector256<uint> r5 = NextLanes(ref a, ref b, ref c, ref d);
                Vector256<uint> r6 = NextLanes(ref a, ref b, ref c, ref d);
                Vector256<uint> r7 = NextLanes(ref a, ref b, ref c, ref d);
                Transpose(ref r0, ref r1, ref r2, ref r3, ref r4, ref r5, ref r6, ref r7);
                r0.Store(buffer + n);
                r1.Store(buffer + RandnConstants.ChunkLength + n);
                r2.Store(buffer + (2 * RandnConstants.ChunkLength) + n);
                r3.Store(buffer + (3 * RandnConstants.ChunkLength) + n);
                r4.Store(buffer + (4 * RandnConstants.ChunkLength) + n);
                r5.Store(buffer + (5 * RandnConstants.ChunkLength) + n);
                r6.Store(buffer + (6 * RandnConstants.ChunkLength) + n);
                r7.Store(buffer + (7 * RandnConstants.ChunkLength) + n);
            }
            Jump(a, b, c, d, lanes);
        }
        _index = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<uint> NextLanes(ref Vector256<uint> a, ref Vector256<uint> b,
        ref Vector256<uint> c, ref Vector256<uint> d)
    {
        Vector256<uint> accumulator = Vector256<uint>.Zero;
        for (int i = 0; i < 3; ++i)
        {
            a = Advance(a, d);
            accumulator += Vector256.ShiftRightLogical(a, 4);
            b = Advance(b, a);
            accumulator += Vector256.ShiftRightLogical(b, 4);
            c = Advance(c, b);
            accumulator += Vector256.ShiftRightLogical(c, 4);
            d = Advance(d, c);
            accumulator += Vector256.ShiftRightLogical(d, 4);
        }
        return accumulator;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<uint> Advance(Vector256<uint> current, Vector256<uint> previous)
    {
        Vector256<uint> t = current ^ Vector256.ShiftLeft(current, 11);
        return (previous ^ Vector256.ShiftRightLogical(previous, 19)) ^
            (t ^ Vector256.ShiftRightLogical(t, 8));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Transpose(ref Vector256<uint> r0, ref Vector256<uint> r1,
        ref Vector256<uint> r2, ref Vector256<uint> r3, ref Vector256<uint> r4,
        ref Vector256<uint> r5, ref Vector256<uint> r6, ref Vector256<uint> r7)
    {
        Vector256<uint> t0 = Avx2.UnpackLow(r0, r1);
        Vector256<uint> t1 = Avx2.UnpackHigh(r0, r1);
        Vector256<uint> t2 = Avx2.UnpackLow(r2, r3);
        Vector256<uint> t3 = Avx2.UnpackHigh(r2, r3);
        Vector256<uint> t4 = Avx2.UnpackLow(r4, r5);
        Vector256<uint> t5 = Avx2.UnpackHigh(r4, r5);
        Vector256<uint> t6 = Avx2.UnpackLow(r6, r7);
        Vector256<uint> t7 = Avx2.UnpackHigh(r6, r7);
        Vector256<ulong> u0 = Avx2.UnpackLow(t0.AsUInt64(), t2.AsUInt64());
        Vector256<ulong> u1 = Avx2.UnpackHigh(t0.AsUInt64(), t2.AsUInt64());
        Vector256<ulong> u2 = Avx2.UnpackLow(t1.AsUInt64(), t3.AsUInt64());
        Vector256<ulong> u3 = Avx2.UnpackHigh(t1.AsUInt64(), t3.AsUInt64());
        Vector256<ulong> u4 = Avx2.UnpackLow(t4.AsUInt64(), t6.AsUInt64());
        Vector256<ulong> u5 = Avx2.UnpackHigh(t4.AsUInt64(), t6.AsUInt64());
        Vector256<ulong> u6 = Avx2.UnpackLow(t5.AsUInt64(), t7.AsUInt64());
        Vector256<ulong> u7 = Avx2.UnpackHigh(t5.AsUInt64(), t7.AsUInt64());
        r0 = Avx2.Permute2x128(u0, u4, 0x20).AsUInt32();
        r1 = Avx2.Permute2x128(u1, u5, 0x20).AsUInt32();
        r2 = Avx2.Permute2x128(u2, u6, 0x20).AsUInt32();
        r3 = Avx2.Permute2x128(u3, u7, 0x20).AsUInt32();
        r4 = Avx2.Permute2x128(u0, u4, 0x31).AsUInt32();
        r5 = Avx2.Permute2x128(u1, u5, 0x31).AsUInt32();
        r6 = Avx2.Permute2x128(u2, u6, 0x31).AsUInt32();
        r7 = Avx2.Permute2x128(u3, u7, 0x31).AsUInt32();
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void Jump(Vector256<uint> a, Vector256<uint> b, Vector256<uint> c,
        Vector256<uint> d, uint* lanes)
    {
        Vector256<uint> r0 = Vector256<uint>.Zero;
        Vector256<uint> r1 = Vector256<uint>.Zero;
        Vector256<uint> r2 = Vector256<uint>.Zero;
        Vector256<uint> r3 = Vector256<uint>.Zero;
        fixed (uint* columns = RandnConstants.JumpColumns)
        {
            JumpWord(a, columns, ref r0, ref r1, ref r2, ref r3);
            JumpWord(b, columns + 128, ref r0, ref r1, ref r2, ref r3);
            JumpWord(c, columns + 256, ref r0, ref r1, ref r2, ref r3);
            JumpWord(d, columns + 384, ref r0, ref r1, ref r2, ref r3);
        }
        r0.Store(lanes);
        r1.Store(lanes + RandnConstants.Lanes);
        r2.Store(lanes + (2 * RandnConstants.Lanes));
        r3.Store(lanes + (3 * RandnConstants.Lanes));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void JumpWord(Vector256<uint> word, uint* columns, ref Vector256<uint> r0,
        ref Vector256<uint> r1, ref Vector256<uint> r2, ref Vector256<uint> r3)
    {
        for (int bit = 0; bit < 32; ++bit)
        {
            Vector256<uint> mask = Vector256<uint>.Zero -
                (Vector256.ShiftRightLogical(word, bit) & Vector256<uint>.One);
            uint* column = columns + (bit * 4);
            r0 ^= mask & Vector256.Create(column[0]);
            r1 ^= mask & Vector256.Create(column[1]);
            r2 ^= mask & Vector256.Create(column[2]);
            r3 ^= mask & Vector256.Create(column[3]);
        }
    }
}
