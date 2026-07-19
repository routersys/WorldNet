namespace WorldNet;

internal struct RandnState
{
    public uint X;
    public uint Y;
    public uint Z;
    public uint W;

    public void Reseed()
    {
        X = 123456789;
        Y = 362436069;
        Z = 521288629;
        W = 88675123;
    }

    public double Next()
    {
        uint t = X ^ (X << 11);
        X = Y;
        Y = Z;
        Z = W;
        W = (W ^ (W >> 19)) ^ (t ^ (t >> 8));

        uint accumulator = W >> 4;
        for (int i = 0; i < 11; ++i)
        {
            t = X ^ (X << 11);
            X = Y;
            Y = Z;
            Z = W;
            W = (W ^ (W >> 19)) ^ (t ^ (t >> 8));
            accumulator += W >> 4;
        }

        return accumulator / 268435456.0 - 6.0;
    }
}
