namespace WorldNet;

public sealed unsafe class WorldSynthesizer
{
    private readonly int _fs;
    private readonly double _framePeriod;
    private readonly int _bufferSize;
    private readonly int _numberOfPointers;
    private readonly int _fftSize;
    private readonly int _spectrumLength;
    private readonly int _maxFrames;
    private readonly int _maxSamples;

    private readonly double* _buffer;
    private readonly double* _dcRemover;
    private readonly double* _impulseResponse;
    private readonly int* _f0Length;
    private readonly int* _f0Origin;
    private readonly int* _numberOfPulses;
    private readonly double* _spectrogramStorage;
    private readonly double* _aperiodicityStorage;
    private readonly double* _interpolatedVuv;
    private readonly double* _pulseLocations;
    private readonly int* _pulseLocationsIndex;

    private readonly double* _aperiodicResponse;
    private readonly double* _periodicResponse;
    private readonly double* _spectralEnvelope;
    private readonly double* _aperiodicRatio;
    private readonly double* _coarseTimeAxis;
    private readonly double* _coarseF0;
    private readonly double* _coarseVuv;
    private readonly double* _interpolatedF0;
    private readonly double* _timeAxis;
    private readonly double* _totalPhase;
    private readonly double* _wrapPhase;
    private readonly double* _wrapPhaseAbs;
    private readonly Interp1Scratch _interpolation;

    private MinimumPhaseAnalysis _minimumPhase;
    private InverseRealFft _inverseRealFft;
    private ForwardRealFft _forwardRealFft;
    private RandnState _randnState;

    private int _currentPointer;
    private int _currentPointer2;
    private int _headPointer;
    private int _pulseIndex;
    private int _synthesizedSample;
    private int _handoff;
    private double _handoffPhase;
    private double _handoffF0;
    private int _lastLocation;
    private int _cumulativeFrame;

    public WorldSynthesizer(WorldArena arena, int fs, double framePeriod, int fftSize,
        int bufferSize, int numberOfPointers, int maxFramesPerAdd)
    {
        ArgumentNullException.ThrowIfNull(arena);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fs);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(framePeriod);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fftSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bufferSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(numberOfPointers);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxFramesPerAdd);

        _fs = fs;
        _framePeriod = framePeriod / 1000.0;
        _bufferSize = bufferSize;
        _numberOfPointers = numberOfPointers;
        _fftSize = fftSize;
        _spectrumLength = (fftSize / 2) + 1;
        _maxFrames = maxFramesPerAdd;
        _maxSamples = (int)Math.Ceiling(maxFramesPerAdd * _framePeriod * fs) + 2;

        _f0Length = (int*)arena.AllocateRaw(numberOfPointers, sizeof(int));
        _f0Origin = (int*)arena.AllocateRaw(numberOfPointers, sizeof(int));
        _numberOfPulses = (int*)arena.AllocateRaw(numberOfPointers, sizeof(int));
        _spectrogramStorage = (double*)arena.AllocateRaw(
            numberOfPointers * _maxFrames * _spectrumLength, sizeof(double));
        _aperiodicityStorage = (double*)arena.AllocateRaw(
            numberOfPointers * _maxFrames * _spectrumLength, sizeof(double));
        _interpolatedVuv = (double*)arena.AllocateRaw(
            numberOfPointers * (_maxSamples + 1), sizeof(double));
        _pulseLocations =
            (double*)arena.AllocateRaw(numberOfPointers * _maxSamples, sizeof(double));
        _pulseLocationsIndex =
            (int*)arena.AllocateRaw(numberOfPointers * _maxSamples, sizeof(int));

        _buffer = (double*)arena.AllocateRaw((bufferSize * 2) + fftSize, sizeof(double));
        _impulseResponse = (double*)arena.AllocateRaw(fftSize, sizeof(double));
        _dcRemover = (double*)arena.AllocateRaw(fftSize / 2, sizeof(double));

        _aperiodicResponse = (double*)arena.AllocateRaw(fftSize, sizeof(double));
        _periodicResponse = (double*)arena.AllocateRaw(fftSize, sizeof(double));
        _spectralEnvelope = (double*)arena.AllocateRaw(fftSize, sizeof(double));
        _aperiodicRatio = (double*)arena.AllocateRaw(fftSize, sizeof(double));
        _coarseTimeAxis = (double*)arena.AllocateRaw(_maxFrames + 1, sizeof(double));
        _coarseF0 = (double*)arena.AllocateRaw(_maxFrames + 1, sizeof(double));
        _coarseVuv = (double*)arena.AllocateRaw(_maxFrames + 1, sizeof(double));
        _interpolatedF0 = (double*)arena.AllocateRaw(_maxSamples, sizeof(double));
        _timeAxis = (double*)arena.AllocateRaw(_maxSamples, sizeof(double));
        _totalPhase = (double*)arena.AllocateRaw(_maxSamples + 1, sizeof(double));
        _wrapPhase = (double*)arena.AllocateRaw(_maxSamples + 1, sizeof(double));
        _wrapPhaseAbs = (double*)arena.AllocateRaw(_maxSamples + 1, sizeof(double));
        _interpolation = Interp1Scratch.Bind(arena, _maxFrames + 1, _maxSamples);

        Refresh();

        _minimumPhase = MinimumPhaseAnalysis.Bind(arena, fftSize);
        _inverseRealFft = InverseRealFft.Bind(arena, fftSize);
        _forwardRealFft = ForwardRealFft.Bind(arena, fftSize);
    }

    public ReadOnlySpan<double> Buffer => new(_buffer, _bufferSize);

    public bool IsLocked
    {
        get
        {
            int judge = 0;
            if (_headPointer - _currentPointer2 == _numberOfPointers)
            {
                ++judge;
            }
            if (_synthesizedSample + _bufferSize >= _lastLocation)
            {
                ++judge;
            }
            return judge == 2;
        }
    }

    public void Refresh()
    {
        ClearRingBuffer(0, _numberOfPointers);
        _handoffPhase = 0;
        _handoffF0 = 0;
        _cumulativeFrame = -1;
        _lastLocation = 0;

        _currentPointer = 0;
        _currentPointer2 = 0;
        _headPointer = 0;
        _handoff = 0;

        _pulseIndex = 0;

        _synthesizedSample = 0;

        for (int i = 0; i < (_bufferSize * 2) + _fftSize; ++i)
        {
            _buffer[i] = 0;
        }
        GetDCRemover(_fftSize / 2, _dcRemover);
        _randnState.Reseed();
    }

    public bool AddParameters(ReadOnlySpan<double> f0, ReadOnlySpan<double> spectrogram,
        ReadOnlySpan<double> aperiodicity)
    {
        int f0Length = f0.Length;
        ArgumentOutOfRangeException.ThrowIfGreaterThan(f0Length, _maxFrames, nameof(f0));

        if (spectrogram.Length < (long)f0Length * _spectrumLength)
        {
            throw new ArgumentException(
                "The spectrogram is smaller than f0 length times the spectrum length.",
                nameof(spectrogram));
        }

        if (aperiodicity.Length < (long)f0Length * _spectrumLength)
        {
            throw new ArgumentException(
                "The aperiodicity is smaller than f0 length times the spectrum length.",
                nameof(aperiodicity));
        }

        if (_headPointer - _currentPointer2 == _numberOfPointers)
        {
            return false;
        }

        int pointer = _headPointer % _numberOfPointers;
        _f0Length[pointer] = f0Length;
        _f0Origin[pointer] = _cumulativeFrame + 1;
        _cumulativeFrame += f0Length;

        long slotElements = (long)_maxFrames * _spectrumLength;
        spectrogram[..(f0Length * _spectrumLength)].CopyTo(
            new Span<double>(_spectrogramStorage + (pointer * slotElements),
                f0Length * _spectrumLength));
        aperiodicity[..(f0Length * _spectrumLength)].CopyTo(
            new Span<double>(_aperiodicityStorage + (pointer * slotElements),
                f0Length * _spectrumLength));

        fixed (double* f0Pointer = f0)
        {
            if (_cumulativeFrame < 1)
            {
                _handoffF0 = f0Pointer[f0Length - 1];
                _numberOfPulses[pointer] = 0;
                ++_headPointer;
                _handoff = 1;
                return true;
            }

            int startSample = WorldMath.MaxInt(0,
                (int)Math.Ceiling((_cumulativeFrame - f0Length) * _framePeriod * _fs));
            int endSample = (int)Math.Ceiling(_cumulativeFrame * _framePeriod * _fs);
            int numberOfSamples = endSample - startSample;

            if (numberOfSamples > _maxSamples)
            {
                throw new InvalidOperationException(
                    $"The chunk needs {numberOfSamples} samples but the capacity is {_maxSamples}.");
            }

            GetTimeBase(f0Pointer, f0Length, startSample, numberOfSamples);

            _handoffF0 = f0Pointer[f0Length - 1];
            ++_headPointer;
            _handoff = 1;
            return true;
        }
    }

    public bool Synthesize()
    {
        if (!CheckSynthesizer())
        {
            return false;
        }
        for (int i = 0; i < _bufferSize + _fftSize; ++i)
        {
            _buffer[i] = _buffer[i + _bufferSize];
        }

        int pointer = _currentPointer % _numberOfPointers;
        int currentLocation = PulseLocationsIndexSlot(pointer)[_pulseIndex];
        while (currentLocation < _synthesizedSample + _bufferSize)
        {
            int tmp = GetNextPulseLocationIndex();
            int noiseSize = tmp - currentLocation;

            GetOneFrameSegment(noiseSize, currentLocation);
            int offset = currentLocation - _synthesizedSample - (_fftSize / 2) + 1;
            for (int i = WorldMath.MaxInt(0, -offset); i < _fftSize; ++i)
            {
                int index = i + offset;
                _buffer[index] += _impulseResponse[i];
            }
            currentLocation = tmp;
            UpdateSynthesizer();
        }
        _synthesizedSample += _bufferSize;
        SeekSynthesizer(_synthesizedSample);
        return true;
    }

    private double* InterpolatedVuvSlot(int pointer)
    {
        return _interpolatedVuv + ((long)pointer * (_maxSamples + 1));
    }

    private double* PulseLocationsSlot(int pointer)
    {
        return _pulseLocations + ((long)pointer * _maxSamples);
    }

    private int* PulseLocationsIndexSlot(int pointer)
    {
        return _pulseLocationsIndex + ((long)pointer * _maxSamples);
    }

    private double* SpectrogramRow(int pointer, int index)
    {
        return _spectrogramStorage +
            (((long)pointer * _maxFrames * _spectrumLength) + ((long)index * _spectrumLength));
    }

    private double* AperiodicityRow(int pointer, int index)
    {
        return _aperiodicityStorage +
            (((long)pointer * _maxFrames * _spectrumLength) + ((long)index * _spectrumLength));
    }

    private void ClearRingBuffer(int start, int end)
    {
        for (int i = start; i < end; ++i)
        {
            int pointer = i % _numberOfPointers;
            _numberOfPulses[pointer] = 0;
        }
    }

    private void SeekSynthesizer(double currentLocation)
    {
        int frameNumber = (int)(currentLocation / _framePeriod);

        int tmpPointer = _currentPointer2;
        for (int i = 0; i < _headPointer - _currentPointer2; ++i)
        {
            int tmp = (tmpPointer + i) % _numberOfPointers;
            if (_f0Origin[tmp] <= frameNumber &&
                frameNumber < _f0Origin[tmp] + _f0Length[tmp])
            {
                tmpPointer += i;
                break;
            }
        }
        ClearRingBuffer(_currentPointer2, tmpPointer);
        _currentPointer2 = tmpPointer;
    }

    private void SearchPointer(int frame, int flag, double** front, double** next)
    {
        int pointer = _currentPointer2 % _numberOfPointers;
        int index = -1;
        for (int i = 0; i < _f0Length[pointer]; ++i)
        {
            if (_f0Origin[pointer] + i == frame)
            {
                index = i;
                break;
            }
        }

        if (flag == 0)
        {
            *front = SpectrogramRow(pointer, index);
            *next = index == _f0Length[pointer] - 1
                ? SpectrogramRow((_currentPointer2 + 1) % _numberOfPointers, 0)
                : SpectrogramRow(pointer, index + 1);
        }
        else
        {
            *front = AperiodicityRow(pointer, index);
            *next = index == _f0Length[pointer] - 1
                ? AperiodicityRow((_currentPointer2 + 1) % _numberOfPointers, 0)
                : AperiodicityRow(pointer, index + 1);
        }
    }

    private void RemoveDCComponent(double* periodicResponse, double* newPeriodicResponse)
    {
        double dcComponent = 0.0;
        for (int i = _fftSize / 2; i < _fftSize; ++i)
        {
            dcComponent += periodicResponse[i];
        }
        for (int i = 0; i < _fftSize / 2; ++i)
        {
            newPeriodicResponse[i] = 0.0;
        }
        for (int i = _fftSize / 2; i < _fftSize; ++i)
        {
            newPeriodicResponse[i] -= dcComponent * _dcRemover[i - (_fftSize / 2)];
        }
    }

    private void GetNoiseSpectrum(int noiseSize)
    {
        double average = 0.0;
        for (int i = 0; i < noiseSize; ++i)
        {
            _forwardRealFft.Waveform[i] = _randnState.Next();
            average += _forwardRealFft.Waveform[i];
        }

        average /= noiseSize;
        for (int i = 0; i < noiseSize; ++i)
        {
            _forwardRealFft.Waveform[i] -= average;
        }
        for (int i = noiseSize; i < _fftSize; ++i)
        {
            _forwardRealFft.Waveform[i] = 0.0;
        }
        _forwardRealFft.ForwardFft.Execute();
    }

    private void GetAperiodicResponse(int noiseSize, double* spectrum, double* aperiodicRatio,
        double currentVuv, double* aperiodicResponse)
    {
        GetNoiseSpectrum(noiseSize);

        if (currentVuv != 0.0)
        {
            for (int i = 0; i <= _minimumPhase.FftSize / 2; ++i)
            {
                _minimumPhase.LogSpectrum[i] =
                    Math.Log((spectrum[i] * aperiodicRatio[i]) +
                    WorldConstants.MySafeGuardMinimum) / 2.0;
            }
        }
        else
        {
            for (int i = 0; i <= _minimumPhase.FftSize / 2; ++i)
            {
                _minimumPhase.LogSpectrum[i] = Math.Log(spectrum[i]) / 2.0;
            }
        }
        _minimumPhase.GetMinimumPhaseSpectrum();

        for (int i = 0; i <= _fftSize / 2; ++i)
        {
            _inverseRealFft.Spectrum[i].Real =
                (_minimumPhase.MinimumPhaseSpectrum[i].Real *
                    _forwardRealFft.Spectrum[i].Real) -
                (_minimumPhase.MinimumPhaseSpectrum[i].Imaginary *
                    _forwardRealFft.Spectrum[i].Imaginary);
            _inverseRealFft.Spectrum[i].Imaginary =
                (_minimumPhase.MinimumPhaseSpectrum[i].Real *
                    _forwardRealFft.Spectrum[i].Imaginary) +
                (_minimumPhase.MinimumPhaseSpectrum[i].Imaginary *
                    _forwardRealFft.Spectrum[i].Real);
        }
        _inverseRealFft.InverseFft.Execute();
        MatlabFunctions.FftShift(_inverseRealFft.Waveform, _fftSize, aperiodicResponse);
    }

    private void GetPeriodicResponse(double* spectrum, double* aperiodicRatio, double currentVuv,
        double* periodicResponse)
    {
        if (currentVuv <= 0.5 || aperiodicRatio[0] > 0.999)
        {
            for (int i = 0; i < _fftSize; ++i)
            {
                periodicResponse[i] = 0.0;
            }
            return;
        }

        for (int i = 0; i <= _minimumPhase.FftSize / 2; ++i)
        {
            _minimumPhase.LogSpectrum[i] =
                Math.Log((spectrum[i] * (1.0 - aperiodicRatio[i])) +
                WorldConstants.MySafeGuardMinimum) / 2.0;
        }
        _minimumPhase.GetMinimumPhaseSpectrum();

        for (int i = 0; i <= _fftSize / 2; ++i)
        {
            _inverseRealFft.Spectrum[i].Real = _minimumPhase.MinimumPhaseSpectrum[i].Real;
            _inverseRealFft.Spectrum[i].Imaginary =
                _minimumPhase.MinimumPhaseSpectrum[i].Imaginary;
        }

        _inverseRealFft.InverseFft.Execute();
        MatlabFunctions.FftShift(_inverseRealFft.Waveform, _fftSize, periodicResponse);
        RemoveDCComponent(periodicResponse, periodicResponse);
    }

    private void GetSpectralEnvelope(double currentLocation, double* spectralEnvelope)
    {
        int currentFrameFloor = (int)(currentLocation / _framePeriod);
        int currentFrameCeil = (int)Math.Ceiling(currentLocation / _framePeriod);
        double interpolation = (currentLocation / _framePeriod) - currentFrameFloor;

        double* front = null;
        double* next = null;
        SearchPointer(currentFrameFloor, 0, &front, &next);

        if (currentFrameFloor == currentFrameCeil)
        {
            for (int i = 0; i <= _fftSize / 2; ++i)
            {
                spectralEnvelope[i] = Math.Abs(front[i]);
            }
        }
        else
        {
            for (int i = 0; i <= _fftSize / 2; ++i)
            {
                spectralEnvelope[i] = ((1.0 - interpolation) * Math.Abs(front[i])) +
                    (interpolation * Math.Abs(next[i]));
            }
        }
    }

    private void GetAperiodicRatio(double currentLocation, double* aperiodicSpectrum)
    {
        int currentFrameFloor = (int)(currentLocation / _framePeriod);
        int currentFrameCeil = (int)Math.Ceiling(currentLocation / _framePeriod);
        double interpolation = (currentLocation / _framePeriod) - currentFrameFloor;

        double* front = null;
        double* next = null;
        SearchPointer(currentFrameFloor, 1, &front, &next);

        if (currentFrameFloor == currentFrameCeil)
        {
            for (int i = 0; i <= _fftSize / 2; ++i)
            {
                double safe = WorldMath.GetSafeAperiodicity(front[i]);
                aperiodicSpectrum[i] = safe * safe;
            }
        }
        else
        {
            for (int i = 0; i <= _fftSize / 2; ++i)
            {
                double blended = ((1.0 - interpolation) * WorldMath.GetSafeAperiodicity(front[i])) +
                    (interpolation * WorldMath.GetSafeAperiodicity(next[i]));
                aperiodicSpectrum[i] = blended * blended;
            }
        }
    }

    private double GetCurrentVUV(int currentLocation)
    {
        int pointer = _currentPointer % _numberOfPointers;

        int startSample = WorldMath.MaxInt(0,
            (int)Math.Ceiling((_f0Origin[pointer] - 1) * _framePeriod * _fs));

        return InterpolatedVuvSlot(pointer)[currentLocation - startSample + 1];
    }

    private void GetOneFrameSegment(int noiseSize, int currentLocation)
    {
        double tmpLocation = (double)currentLocation / _fs;
        SeekSynthesizer(tmpLocation);
        GetSpectralEnvelope(tmpLocation, _spectralEnvelope);
        GetAperiodicRatio(tmpLocation, _aperiodicRatio);

        double currentVuv = GetCurrentVUV(currentLocation);

        GetPeriodicResponse(_spectralEnvelope, _aperiodicRatio, currentVuv, _periodicResponse);

        GetAperiodicResponse(noiseSize, _spectralEnvelope, _aperiodicRatio, currentVuv,
            _aperiodicResponse);

        double sqrtNoiseSize = Math.Sqrt(noiseSize);
        for (int i = 0; i < _fftSize; ++i)
        {
            _impulseResponse[i] =
                ((_periodicResponse[i] * sqrtNoiseSize) + _aperiodicResponse[i]) / _fftSize;
        }
    }

    private void GetTemporalParametersForTimeBase(double* f0, int f0Length)
    {
        int cumulativeFrame = WorldMath.MaxInt(0, _cumulativeFrame - f0Length);
        _coarseF0[0] = _handoffF0;
        _coarseTimeAxis[0] = cumulativeFrame * _framePeriod;
        _coarseVuv[0] = _handoffF0 == 0 ? 0.0 : 1.0;
        for (int i = 0; i < f0Length; ++i)
        {
            _coarseTimeAxis[i + _handoff] = (i + cumulativeFrame + _handoff) * _framePeriod;
            _coarseF0[i + _handoff] = f0[i];
            _coarseVuv[i + _handoff] = f0[i] == 0.0 ? 0.0 : 1.0;
        }
    }

    private void GetPulseLocationsForTimeBase(double* interpolatedF0, int numberOfSamples)
    {
        _totalPhase[0] = _handoff == 1
            ? _handoffPhase
            : 2.0 * WorldConstants.Pi * interpolatedF0[0] / _fs;

        _totalPhase[1] = _totalPhase[0] + (2.0 * WorldConstants.Pi * interpolatedF0[0] / _fs);
        for (int i = 1 + _handoff; i < numberOfSamples + _handoff; ++i)
        {
            _totalPhase[i] = _totalPhase[i - 1] +
                (2.0 * WorldConstants.Pi * interpolatedF0[i - _handoff] / _fs);
        }
        _handoffPhase = _totalPhase[numberOfSamples - 1 + _handoff];

        for (int i = 0; i < numberOfSamples + _handoff; ++i)
        {
            _wrapPhase[i] = _totalPhase[i] % (2.0 * WorldConstants.Pi);
        }

        for (int i = 0; i < numberOfSamples - 1 + _handoff; ++i)
        {
            _wrapPhaseAbs[i] = Math.Abs(_wrapPhase[i + 1] - _wrapPhase[i]);
        }

        int pointer = _headPointer % _numberOfPointers;
        double* pulseLocations = PulseLocationsSlot(pointer);
        int* pulseLocationsIndex = PulseLocationsIndexSlot(pointer);
        int numberOfPulses = 0;
        for (int i = 0; i < numberOfSamples - 1 + _handoff; ++i)
        {
            if (_wrapPhaseAbs[i] > WorldConstants.Pi)
            {
                pulseLocations[numberOfPulses] =
                    _timeAxis[i] - ((double)_handoff / _fs);
                pulseLocationsIndex[numberOfPulses] =
                    MatlabFunctions.MatlabRound(pulseLocations[numberOfPulses] * _fs);
                ++numberOfPulses;
            }
        }
        _numberOfPulses[pointer] = numberOfPulses;

        if (numberOfPulses != 0)
        {
            _lastLocation = pulseLocationsIndex[numberOfPulses - 1];
        }
    }

    private void GetTimeBase(double* f0, int f0Length, int startSample, int numberOfSamples)
    {
        GetTemporalParametersForTimeBase(f0, f0Length);

        for (int i = 0; i < numberOfSamples; ++i)
        {
            _timeAxis[i] = (i + startSample) / (double)_fs;
        }

        int pointer = _headPointer % _numberOfPointers;
        double* interpolatedVuv = InterpolatedVuvSlot(pointer);
        MatlabFunctions.Interp1(_coarseTimeAxis, _coarseF0, f0Length + _handoff, _timeAxis,
            numberOfSamples, _interpolatedF0, _interpolation);
        MatlabFunctions.Interp1(_coarseTimeAxis, _coarseVuv, f0Length + _handoff, _timeAxis,
            numberOfSamples, interpolatedVuv, _interpolation);
        for (int i = 0; i < numberOfSamples; ++i)
        {
            interpolatedVuv[i] = interpolatedVuv[i] > 0.5 ? 1.0 : 0.0;
            _interpolatedF0[i] =
                interpolatedVuv[i] == 0.0 ? WorldConstants.DefaultF0 : _interpolatedF0[i];
        }

        GetPulseLocationsForTimeBase(_interpolatedF0, numberOfSamples);

        _handoffF0 = _interpolatedF0[numberOfSamples - 1];
    }

    private int GetNextPulseLocationIndex()
    {
        int pointer = _currentPointer % _numberOfPointers;
        if (_pulseIndex < _numberOfPulses[pointer] - 1)
        {
            return PulseLocationsIndexSlot(pointer)[_pulseIndex + 1];
        }
        else if (_currentPointer == _headPointer - 1)
        {
            return 0;
        }

        for (int i = 1; i < _numberOfPointers; ++i)
        {
            pointer = (i + _currentPointer) % _numberOfPointers;
            if (_numberOfPulses[pointer] != 0)
            {
                return PulseLocationsIndexSlot(pointer)[0];
            }
        }
        return 0;
    }

    private void UpdateSynthesizer()
    {
        int pointer = _currentPointer % _numberOfPointers;
        if (_pulseIndex < _numberOfPulses[pointer] - 1)
        {
            ++_pulseIndex;
            return;
        }
        else
        {
            if (_currentPointer == _headPointer - 1)
            {
                return;
            }
        }

        for (int i = 1; i < _numberOfPointers; ++i)
        {
            pointer = (i + _currentPointer) % _numberOfPointers;
            if (_numberOfPulses[pointer] != 0)
            {
                _pulseIndex = 0;
                _currentPointer += i;
                return;
            }
        }
    }

    private bool CheckSynthesizer()
    {
        if (_synthesizedSample + _bufferSize >= _lastLocation)
        {
            return false;
        }

        int pointer = _currentPointer % _numberOfPointers;
        while (_numberOfPulses[pointer] == 0)
        {
            if (_currentPointer == _headPointer)
            {
                break;
            }
            ++_currentPointer;
            pointer = _currentPointer % _numberOfPointers;
        }
        return true;
    }

    private static void GetDCRemover(int fftSize, double* dcRemover)
    {
        double dcComponent = 0.0;
        for (int i = 0; i < fftSize / 2; ++i)
        {
            dcRemover[i] = 0.5 -
                (0.5 * Math.Cos(2.0 * WorldConstants.Pi * (i + 1.0) / (1.0 + fftSize)));
            dcRemover[fftSize - i - 1] = dcRemover[i];
            dcComponent += dcRemover[i] * 2.0;
        }
        for (int i = 0; i < fftSize / 2; ++i)
        {
            dcRemover[i] /= dcComponent;
            dcRemover[fftSize - i - 1] = dcRemover[i];
        }
    }
}
