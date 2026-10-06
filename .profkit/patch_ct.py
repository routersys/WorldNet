import sys
src = sys.argv[1]
s = open(src + "/CheapTrick.cs").read()
def rep(old, new, count=1):
    global s
    assert s.count(old) == count, (old, s.count(old))
    s = s.replace(old, new)
rep("using System.Runtime.CompilerServices;", "using System.Diagnostics;\nusing System.Runtime.CompilerServices;")
rep("""        double* smoothingLifter = scratch.SmoothingLifter;
        double* compensationLifter = scratch.CompensationLifter;
""", """        long _t = Stopwatch.GetTimestamp();
        double* smoothingLifter = scratch.SmoothingLifter;
        double* compensationLifter = scratch.CompensationLifter;
""")
rep("""        double* waveform = scratch.ForwardRealFft.Waveform;
        for (int i = 0; i <= fftSize / 2; ++i)
        {
            waveform[i] = Math.Log(waveform[i]);
        }
        for (int i = 1; i < fftSize / 2; ++i)
        {
            waveform[fftSize - i] = waveform[i];
        }
        scratch.ForwardRealFft.ForwardFft.Execute();
""", """        Prof.PT(6, ref _t);
        double* waveform = scratch.ForwardRealFft.Waveform;
        for (int i = 0; i <= fftSize / 2; ++i)
        {
            waveform[i] = Math.Log(waveform[i]);
        }
        for (int i = 1; i < fftSize / 2; ++i)
        {
            waveform[fftSize - i] = waveform[i];
        }
        Prof.PT(7, ref _t);
        scratch.ForwardRealFft.ForwardFft.Execute();
        Prof.PT(8, ref _t);
""")
rep("""        scratch.InverseRealFft.InverseFft.Execute();

        double* inverseWaveform = scratch.InverseRealFft.Waveform;
        double* spectralEnvelope = scratch.SpectralEnvelope;
        for (int i = 0; i <= fftSize / 2; ++i)
        {
            spectralEnvelope[i] = Math.Exp(inverseWaveform[i]);
        }""", """        Prof.PT(9, ref _t);
        scratch.InverseRealFft.InverseFft.Execute();
        Prof.PT(10, ref _t);

        double* inverseWaveform = scratch.InverseRealFft.Waveform;
        double* spectralEnvelope = scratch.SpectralEnvelope;
        for (int i = 0; i <= fftSize / 2; ++i)
        {
            spectralEnvelope[i] = Math.Exp(inverseWaveform[i]);
        }
        Prof.PT(11, ref _t);""")
rep("""        int halfWindowLength = MatlabFunctions.MatlabRound(1.5 * fs / f0);

        double* waveform = scratch.ForwardRealFft.Waveform;
        for (int i = (halfWindowLength * 2) + 1; i < fftSize; ++i)
        {
            waveform[i] = 0.0;
        }
        scratch.ForwardRealFft.ForwardFft.Execute();

        double* powerSpectrum = waveform;
        FftComplex* spectrum = scratch.ForwardRealFft.Spectrum;
        SpectrumMath.PowerSpectrum(spectrum, powerSpectrum, (fftSize / 2) + 1);

        Common.DcCorrection(powerSpectrum, f0, fs, fftSize, powerSpectrum, frame.DcCorrection);""", """        long _t = Stopwatch.GetTimestamp();
        int halfWindowLength = MatlabFunctions.MatlabRound(1.5 * fs / f0);

        double* waveform = scratch.ForwardRealFft.Waveform;
        for (int i = (halfWindowLength * 2) + 1; i < fftSize; ++i)
        {
            waveform[i] = 0.0;
        }
        scratch.ForwardRealFft.ForwardFft.Execute();
        Prof.PT(2, ref _t);

        double* powerSpectrum = waveform;
        FftComplex* spectrum = scratch.ForwardRealFft.Spectrum;
        SpectrumMath.PowerSpectrum(spectrum, powerSpectrum, (fftSize / 2) + 1);
        Prof.PT(3, ref _t);

        Common.DcCorrection(powerSpectrum, f0, fs, fftSize, powerSpectrum, frame.DcCorrection);
        Prof.PT(13, ref _t);""")
rep("""        int* baseIndex, int* safeIndex,
        double* window)
    {
        for (int i = -halfWindowLength; i <= halfWindowLength; ++i)""", """        int* baseIndex, int* safeIndex,
        double* window)
    {
        long _t = Stopwatch.GetTimestamp();
        for (int i = -halfWindowLength; i <= halfWindowLength; ++i)""") if False else None
rep("""        double currentPosition, int fs, double currentF0, int* baseIndex, int* safeIndex,
        double* window)
    {
        for (int i = -halfWindowLength; i <= halfWindowLength; ++i)""", """        double currentPosition, int fs, double currentF0, int* baseIndex, int* safeIndex,
        double* window)
    {
        long _t = Stopwatch.GetTimestamp();
        for (int i = -halfWindowLength; i <= halfWindowLength; ++i)""")
rep("""        VectorOperations.DivideByScalar(window, (halfWindowLength * 2) + 1, average);
    }""", """        VectorOperations.DivideByScalar(window, (halfWindowLength * 2) + 1, average);
        Prof.PT(0, ref _t);
    }""")
rep("""        double* waveform = scratch.ForwardRealFft.Waveform;
        for (int i = 0; i <= halfWindowLength * 2; ++i)
        {
            waveform[i] = (x[safeIndex[i]] * window[i]) +""", """        long _t = Stopwatch.GetTimestamp();
        double* waveform = scratch.ForwardRealFft.Waveform;
        for (int i = 0; i <= halfWindowLength * 2; ++i)
        {
            waveform[i] = (x[safeIndex[i]] * window[i]) +""")
rep("""        VectorOperations.SubtractScaled(
            waveform, window, (halfWindowLength * 2) + 1, weightingCoefficient);
    }""", """        VectorOperations.SubtractScaled(
            waveform, window, (halfWindowLength * 2) + 1, weightingCoefficient);
        Prof.PT(1, ref _t);
    }""")
rep("""        double* outputSpectrum, ref RandnState randnState)
    {
        for (int i = 0; i <= fftSize / 2; ++i)
        {
            outputSpectrum[i] =
                inputSpectrum[i] + (Math.Abs(randnState.Next()) * WorldConstants.Eps);
        }
    }""", """        double* outputSpectrum, ref RandnState randnState)
    {
        long _t = Stopwatch.GetTimestamp();
        for (int i = 0; i <= fftSize / 2; ++i)
        {
            outputSpectrum[i] =
                inputSpectrum[i] + (Math.Abs(randnState.Next()) * WorldConstants.Eps);
        }
        Prof.PT(5, ref _t);
    }""")
rep("""        Common.LinearSmoothing(scratch.ForwardRealFft.Waveform, currentF0 * 2.0 / 3.0, fs, fftSize,
            scratch.ForwardRealFft.Waveform, frame.LinearSmoothing);
""", """        long _t = Stopwatch.GetTimestamp();
        Common.LinearSmoothing(scratch.ForwardRealFft.Waveform, currentF0 * 2.0 / 3.0, fs, fftSize,
            scratch.ForwardRealFft.Waveform, frame.LinearSmoothing);
        Prof.PT(4, ref _t);
""")
open(src + "/CheapTrick.cs", "w").write(s)
