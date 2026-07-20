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
