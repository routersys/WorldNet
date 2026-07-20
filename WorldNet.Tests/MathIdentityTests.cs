namespace WorldNet.Tests;

public class MathIdentityTests
{
    private static IEnumerable<double> Arguments()
    {
        for (int i = -20000; i <= 20000; ++i)
        {
            yield return i * 0.003;
        }
        for (int i = 1; i <= 5000; ++i)
        {
            yield return i * 0.0117;
            yield return -i * 0.0117;
        }
    }

    [Fact]
    public void SinCosMatchesSeparateCalls()
    {
        foreach (double x in Arguments())
        {
            (double sine, double cosine) = Math.SinCos(x);
            Assert.Equal(BitConverter.DoubleToInt64Bits(Math.Sin(x)),
                BitConverter.DoubleToInt64Bits(sine));
            Assert.Equal(BitConverter.DoubleToInt64Bits(Math.Cos(x)),
                BitConverter.DoubleToInt64Bits(cosine));
        }
    }

    [Fact]
    public void CosIsBitwiseEven()
    {
        foreach (double x in Arguments())
        {
            Assert.Equal(BitConverter.DoubleToInt64Bits(Math.Cos(x)),
                BitConverter.DoubleToInt64Bits(Math.Cos(-x)));
        }
    }
}
