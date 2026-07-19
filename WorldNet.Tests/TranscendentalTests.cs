namespace WorldNet.Tests;

public class TranscendentalTests
{
    private static void AssertExact(string name, Func<double, double> evaluate)
    {
        double[] input = ReferenceData.Load($"tr_{name}_input").Values;
        double[] expected = ReferenceData.Load($"tr_{name}_output").Values;

        for (int i = 0; i < input.Length; ++i)
        {
            if (BitConverter.DoubleToInt64Bits(expected[i])
                != BitConverter.DoubleToInt64Bits(evaluate(input[i])))
            {
                Assert.Fail(
                    $"{name} index {i}: expected {expected[i]:E17} " +
                    $"but was {evaluate(input[i]):E17}");
            }
        }
    }

    private static void AssertWithinOneUlp(string name, Func<double, double> evaluate)
    {
        double[] input = ReferenceData.Load($"tr_{name}_input").Values;
        double[] expected = ReferenceData.Load($"tr_{name}_output").Values;

        long mismatches = 0;
        long maxUlp = 0;
        for (int i = 0; i < input.Length; ++i)
        {
            long a = BitConverter.DoubleToInt64Bits(expected[i]);
            long b = BitConverter.DoubleToInt64Bits(evaluate(input[i]));
            if (a != b)
            {
                ++mismatches;
                maxUlp = Math.Max(maxUlp, Math.Abs(a - b));
            }
        }

        if (maxUlp > 1)
        {
            Assert.Fail($"{name} maxUlp={maxUlp} exceeds one ULP.");
        }

        if (mismatches * 100 >= input.Length)
        {
            Assert.Fail($"{name} mismatches={mismatches} of {input.Length} exceeds 1 percent.");
        }
    }

    [Fact]
    public void CosMatchesReference()
    {
        AssertExact("cos", Math.Cos);
    }

    [Fact]
    public void SinMatchesReference()
    {
        AssertExact("sin", Math.Sin);
    }

    [Fact]
    public void LogMatchesReference()
    {
        AssertExact("log", Math.Log);
    }

    [Fact]
    public void ExpMatchesReference()
    {
        AssertExact("exp", Math.Exp);
    }

    [Fact]
    public void Log10MatchesReference()
    {
        AssertExact("log10", Math.Log10);
    }

    [Fact]
    public void Pow10MatchesReferenceWithinOneUlp()
    {
        AssertWithinOneUlp("pow10", v => Math.Pow(10.0, v));
    }

    [Fact]
    public void SquareMatchesReferenceWithinOneUlp()
    {
        AssertWithinOneUlp("pow2", v => v * v);
    }
}
