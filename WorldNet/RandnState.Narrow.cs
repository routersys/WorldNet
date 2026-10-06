using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace WorldNet;

internal unsafe partial struct RandnState
{
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void RefillNarrow()
    {
        uint* rows = stackalloc uint[8 * RandnConstants.Lanes];
        fixed (uint* lanes = &_lanes[0])
        fixed (uint* buffer = &_buffer[0])
        {
            Vector128<uint> a0 = Vector128.Load(lanes);
            Vector128<uint> a1 = Vector128.Load(lanes + 4);
            Vector128<uint> b0 = Vector128.Load(lanes + 8);
            Vector128<uint> b1 = Vector128.Load(lanes + 12);
            Vector128<uint> c0 = Vector128.Load(lanes + 16);
            Vector128<uint> c1 = Vector128.Load(lanes + 20);
            Vector128<uint> d0 = Vector128.Load(lanes + 24);
            Vector128<uint> d1 = Vector128.Load(lanes + 28);
            for (int n = 0; n < RandnConstants.ChunkLength; n += 8)
            {
                for (int k = 0; k < 8; ++k)
                {
                    Vector128<uint> accumulator0 = Vector128<uint>.Zero;
                    Vector128<uint> accumulator1 = Vector128<uint>.Zero;
                    for (int i = 0; i < 3; ++i)
                    {
                        a0 = Advance(a0, d0);
                        a1 = Advance(a1, d1);
                        accumulator0 += Vector128.ShiftRightLogical(a0, 4);
                        accumulator1 += Vector128.ShiftRightLogical(a1, 4);
                        b0 = Advance(b0, a0);
                        b1 = Advance(b1, a1);
                        accumulator0 += Vector128.ShiftRightLogical(b0, 4);
                        accumulator1 += Vector128.ShiftRightLogical(b1, 4);
                        c0 = Advance(c0, b0);
                        c1 = Advance(c1, b1);
                        accumulator0 += Vector128.ShiftRightLogical(c0, 4);
                        accumulator1 += Vector128.ShiftRightLogical(c1, 4);
                        d0 = Advance(d0, c0);
                        d1 = Advance(d1, c1);
                        accumulator0 += Vector128.ShiftRightLogical(d0, 4);
                        accumulator1 += Vector128.ShiftRightLogical(d1, 4);
                    }
                    accumulator0.Store(rows + (k * RandnConstants.Lanes));
                    accumulator1.Store(rows + (k * RandnConstants.Lanes) + 4);
                }
                for (int lane = 0; lane < RandnConstants.Lanes; ++lane)
                {
                    uint* destination = buffer + (lane * RandnConstants.ChunkLength) + n;
                    for (int k = 0; k < 8; ++k)
                    {
                        destination[k] = rows[(k * RandnConstants.Lanes) + lane];
                    }
                }
            }
            JumpNarrow(a0, a1, b0, b1, c0, c1, d0, d1, lanes);
        }
        _index = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<uint> Advance(Vector128<uint> current, Vector128<uint> previous)
    {
        Vector128<uint> t = current ^ Vector128.ShiftLeft(current, 11);
        return (previous ^ Vector128.ShiftRightLogical(previous, 19)) ^
            (t ^ Vector128.ShiftRightLogical(t, 8));
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void JumpNarrow(Vector128<uint> a0, Vector128<uint> a1, Vector128<uint> b0,
        Vector128<uint> b1, Vector128<uint> c0, Vector128<uint> c1, Vector128<uint> d0,
        Vector128<uint> d1, uint* lanes)
    {
        Vector128<uint> r00 = Vector128<uint>.Zero;
        Vector128<uint> r01 = Vector128<uint>.Zero;
        Vector128<uint> r02 = Vector128<uint>.Zero;
        Vector128<uint> r03 = Vector128<uint>.Zero;
        Vector128<uint> r10 = Vector128<uint>.Zero;
        Vector128<uint> r11 = Vector128<uint>.Zero;
        Vector128<uint> r12 = Vector128<uint>.Zero;
        Vector128<uint> r13 = Vector128<uint>.Zero;
        fixed (uint* columns = RandnConstants.JumpColumns)
        {
            JumpWordNarrow(a0, columns, ref r00, ref r01, ref r02, ref r03);
            JumpWordNarrow(a1, columns, ref r10, ref r11, ref r12, ref r13);
            JumpWordNarrow(b0, columns + 128, ref r00, ref r01, ref r02, ref r03);
            JumpWordNarrow(b1, columns + 128, ref r10, ref r11, ref r12, ref r13);
            JumpWordNarrow(c0, columns + 256, ref r00, ref r01, ref r02, ref r03);
            JumpWordNarrow(c1, columns + 256, ref r10, ref r11, ref r12, ref r13);
            JumpWordNarrow(d0, columns + 384, ref r00, ref r01, ref r02, ref r03);
            JumpWordNarrow(d1, columns + 384, ref r10, ref r11, ref r12, ref r13);
        }
        r00.Store(lanes);
        r10.Store(lanes + 4);
        r01.Store(lanes + 8);
        r11.Store(lanes + 12);
        r02.Store(lanes + 16);
        r12.Store(lanes + 20);
        r03.Store(lanes + 24);
        r13.Store(lanes + 28);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void JumpWordNarrow(Vector128<uint> word, uint* columns, ref Vector128<uint> r0,
        ref Vector128<uint> r1, ref Vector128<uint> r2, ref Vector128<uint> r3)
    {
        for (int bit = 0; bit < 32; ++bit)
        {
            Vector128<uint> mask = Vector128<uint>.Zero -
                (Vector128.ShiftRightLogical(word, bit) & Vector128<uint>.One);
            uint* column = columns + (bit * 4);
            r0 ^= mask & Vector128.Create(column[0]);
            r1 ^= mask & Vector128.Create(column[1]);
            r2 ^= mask & Vector128.Create(column[2]);
            r3 ^= mask & Vector128.Create(column[3]);
        }
    }
}
