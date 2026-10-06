import sys
src = sys.argv[1]
s = open(src + "/Synthesis.cs").read()
def rep(old, new, count=1):
    global s
    assert s.count(old) == count, (old, s.count(old))
    s = s.replace(old, new)
rep("using System.Runtime.CompilerServices;", "using System.Diagnostics;\nusing System.Runtime.CompilerServices;")
rep("""        double average = 0.0;
        for (int i = 0; i < noiseSize; ++i)
        {
            forwardRealFft.Waveform[i] = randnState.Next();""", """        long _t = Stopwatch.GetTimestamp();
        double average = 0.0;
        for (int i = 0; i < noiseSize; ++i)
        {
            forwardRealFft.Waveform[i] = randnState.Next();""")
rep("""        forwardRealFft.ForwardFft.Execute();
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void GetAperiodicResponse""", """        forwardRealFft.ForwardFft.Execute();
        Prof.PT(5, ref _t);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void GetAperiodicResponse""")
rep("""        GetNoiseSpectrum(noiseSize, fftSize, scratch.ForwardRealFft, ref randnState);

        MinimumPhaseAnalysis minimumPhase = scratch.MinimumPhase;""", """        GetNoiseSpectrum(noiseSize, fftSize, scratch.ForwardRealFft, ref randnState);
        long _t = Stopwatch.GetTimestamp();

        MinimumPhaseAnalysis minimumPhase = scratch.MinimumPhase;""")
rep("""                minimumPhase.LogSpectrum[i] = Math.Log(spectrum[i]) / 2.0;
            }
        }
        minimumPhase.GetMinimumPhaseSpectrum();
""", """                minimumPhase.LogSpectrum[i] = Math.Log(spectrum[i]) / 2.0;
            }
        }
        Prof.PT(6, ref _t);
        minimumPhase.GetMinimumPhaseSpectrum();
        Prof.PT(7, ref _t);
""")
rep("""        inverseRealFft.InverseFft.Execute();
        MatlabFunctions.FftShift(inverseRealFft.Waveform, fftSize, aperiodicResponse);""", """        Prof.PT(8, ref _t);
        inverseRealFft.InverseFft.Execute();
        MatlabFunctions.FftShift(inverseRealFft.Waveform, fftSize, aperiodicResponse);
        Prof.PT(9, ref _t);""")
rep("""        MinimumPhaseAnalysis minimumPhase = scratch.MinimumPhase;
        for (int i = 0; i <= minimumPhase.FftSize / 2; ++i)
        {
            minimumPhase.LogSpectrum[i] =
                Math.Log((spectrum[i] * (1.0 - aperiodicRatio[i])) +
                WorldConstants.MySafeGuardMinimum) / 2.0;
        }
        minimumPhase.GetMinimumPhaseSpectrum();
""", """        long _t = Stopwatch.GetTimestamp();
        MinimumPhaseAnalysis minimumPhase = scratch.MinimumPhase;
        for (int i = 0; i <= minimumPhase.FftSize / 2; ++i)
        {
            minimumPhase.LogSpectrum[i] =
                Math.Log((spectrum[i] * (1.0 - aperiodicRatio[i])) +
                WorldConstants.MySafeGuardMinimum) / 2.0;
        }
        Prof.PT(1, ref _t);
        minimumPhase.GetMinimumPhaseSpectrum();
        Prof.PT(2, ref _t);
""")
rep("""        GetSpectrumWithFractionalTimeShift(fftSize, coefficient, inverseRealFft);

        inverseRealFft.InverseFft.Execute();
        MatlabFunctions.FftShift(inverseRealFft.Waveform, fftSize, periodicResponse);
        RemoveDCComponent(periodicResponse, fftSize, scratch.DcRemover, periodicResponse);""", """        GetSpectrumWithFractionalTimeShift(fftSize, coefficient, inverseRealFft);
        Prof.PT(3, ref _t);

        inverseRealFft.InverseFft.Execute();
        MatlabFunctions.FftShift(inverseRealFft.Waveform, fftSize, periodicResponse);
        RemoveDCComponent(periodicResponse, fftSize, scratch.DcRemover, periodicResponse);
        Prof.PT(4, ref _t);""")
rep("""        double* aperiodicResponse = scratch.AperiodicResponse;
        double* periodicResponse = scratch.PeriodicResponse;""", """        long _t = Stopwatch.GetTimestamp();
        double* aperiodicResponse = scratch.AperiodicResponse;
        double* periodicResponse = scratch.PeriodicResponse;""")
rep("""        GetAperiodicRatio(currentTime, framePeriod, f0Length, aperiodicity, spectrumLength,
            fftSize, aperiodicRatio);
""", """        GetAperiodicRatio(currentTime, framePeriod, f0Length, aperiodicity, spectrumLength,
            fftSize, aperiodicRatio);
        Prof.PT(0, ref _t);
""")
rep("""        double sqrtNoiseSize = Math.Sqrt(noiseSize);
        double* response = scratch.ImpulseResponse;
        for (int i = 0; i < fftSize; ++i)
        {
            response[i] =
                ((periodicResponse[i] * sqrtNoiseSize) + aperiodicResponse[i]) / fftSize;
        }""", """        _t = Stopwatch.GetTimestamp();
        double sqrtNoiseSize = Math.Sqrt(noiseSize);
        double* response = scratch.ImpulseResponse;
        for (int i = 0; i < fftSize; ++i)
        {
            response[i] =
                ((periodicResponse[i] * sqrtNoiseSize) + aperiodicResponse[i]) / fftSize;
        }
        Prof.PT(10, ref _t);""")
rep("""                int offset = scratch.PulseLocationsIndex[i] - (fftSize / 2) + 1;
                int lowerLimit = WorldMath.MaxInt(0, -offset);
                int upperLimit = WorldMath.MinInt(fftSize, yLength - offset);
                for (int j = lowerLimit; j < upperLimit; ++j)
                {
                    int index = j + offset;
                    yPointer[index] += scratch.ImpulseResponse[j];
                }""", """                long _t = Stopwatch.GetTimestamp();
                int offset = scratch.PulseLocationsIndex[i] - (fftSize / 2) + 1;
                int lowerLimit = WorldMath.MaxInt(0, -offset);
                int upperLimit = WorldMath.MinInt(fftSize, yLength - offset);
                for (int j = lowerLimit; j < upperLimit; ++j)
                {
                    int index = j + offset;
                    yPointer[index] += scratch.ImpulseResponse[j];
                }
                Prof.PT(11, ref _t);""")
open(src + "/Synthesis.cs", "w").write(s)

m = open(src + "/MinimumPhaseAnalysis.cs").read()
def repm(old, new):
    global m
    assert m.count(old) == 1, old
    m = m.replace(old, new)
repm("using System.Runtime.CompilerServices;", "using System.Diagnostics;\nusing System.Runtime.CompilerServices;")
repm("""    {
        for (int i = (FftSize / 2) + 1; i < FftSize; ++i)
        {
            LogSpectrum[i] = LogSpectrum[FftSize - i];
        }

        InverseFft.Execute();
""", """    {
        long _t = Stopwatch.GetTimestamp();
        for (int i = (FftSize / 2) + 1; i < FftSize; ++i)
        {
            LogSpectrum[i] = LogSpectrum[FftSize - i];
        }

        Prof.PT(14, ref _t);
        InverseFft.Execute();
        Prof.PT(15, ref _t);
""")
repm("""        ForwardFft.Execute();

        double inverseFftSize""", """        Prof.PT(16, ref _t);
        ForwardFft.Execute();
        Prof.PT(17, ref _t);

        double inverseFftSize""")
repm("""            MinimumPhaseSpectrum[i].Imaginary = tmp * sine;
        }
    }""", """            MinimumPhaseSpectrum[i].Imaginary = tmp * sine;
        }
        Prof.PT(18, ref _t);
    }""")
open(src + "/MinimumPhaseAnalysis.cs", "w").write(m)
