namespace WorldNet.Tests;

public class TranscendentalTests
{
    [Fact]
    public void Log10MatchesReference()
    {
        double[] input = ReferenceData.Load("tr_log10_input").Values;
        double[] expected = ReferenceData.Load("tr_log10_output").Values;

        for (int i = 0; i < input.Length; ++i)
        {
            if (BitConverter.DoubleToInt64Bits(expected[i])
                != BitConverter.DoubleToInt64Bits(Math.Log10(input[i])))
            {
                Assert.Fail(
                    $"log10 index {i}: expected {expected[i]:E17} " +
                    $"but was {Math.Log10(input[i]):E17}");
            }
        }
    }

    [Fact]
    public void Pow10MatchesReferenceWithinOneUlp()
    {
        double[] input = ReferenceData.Load("tr_pow10_input").Values;
        double[] expected = ReferenceData.Load("tr_pow10_output").Values;

        long mismatches = 0;
        long maxUlp = 0;
        for (int i = 0; i < input.Length; ++i)
        {
            long a = BitConverter.DoubleToInt64Bits(expected[i]);
            long b = BitConverter.DoubleToInt64Bits(Math.Pow(10.0, input[i]));
            if (a != b)
            {
                ++mismatches;
                maxUlp = Math.Max(maxUlp, Math.Abs(a - b));
            }
        }

        if (maxUlp > 1)
        {
            Assert.Fail($"maxUlp={maxUlp} exceeds the one ULP tolerance of Math.Pow.");
        }

        if (mismatches * 1000 >= input.Length)
        {
            Assert.Fail($"mismatches={mismatches} of {input.Length} exceeds 0.1 percent.");
        }
    }
}
