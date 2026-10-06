# WorldNet

[![License](https://img.shields.io/badge/license-MIT-blue.svg)](#)
[![.NET](https://img.shields.io/badge/.NET-8.0%2B-purple.svg)](#)
[![Release](https://img.shields.io/github/v/release/routersys/WorldNet.svg)](https://github.com/routersys/WorldNet/releases)

English | [日本語](https://github.com/routersys/WorldNet/blob/main/README.ja.md)

---

A complete C# port of [WORLD](https://github.com/mmorise/World), the vocoder-based speech analysis, manipulation and synthesis system by M. Morise.
Every stage runs without a single managed allocation, so the garbage collector never observes the analysis or the synthesis path.
All scratch memory comes from a native arena, every hot routine is written with `unsafe` pointers, and the whole library is annotated for Native AOT.
Correctness is not asserted from reading the source: each stage is compared against golden data produced by the original C++ compiled with MSVC on Windows and with GCC on Linux.

---

## Table of Contents

1. [Overview](#overview)
2. [Requirements](#requirements)
3. [Installation](#installation)
4. [Usage](#usage)
5. [Features](#features)
   - [1. Fundamental frequency estimation](#1-fundamental-frequency-estimation)
   - [2. Spectral envelope and aperiodicity](#2-spectral-envelope-and-aperiodicity)
   - [3. Waveform synthesis](#3-waveform-synthesis)
   - [4. Spectral envelope coding](#4-spectral-envelope-coding)
   - [5. Zero allocation and the arena](#5-zero-allocation-and-the-arena)
   - [6. Numerical verification](#6-numerical-verification)
   - [7. Performance](#7-performance)
6. [API Reference](#api-reference)
   - [Analysis](#analysis)
   - [Synthesis](#synthesis)
   - [Coding](#coding)
   - [Memory](#memory)
   - [File I/O](#file-io)
   - [Option defaults](#option-defaults)
7. [Building from source](#building-from-source)
8. [Limitations](#limitations)
9. [Notes](#notes)
10. [Reporting issues](#reporting-issues)
11. [Disclaimer](#disclaimer)
12. [Third-Party Licenses](#third-party-licenses)
13. [License](#license)

---

## Overview

WorldNet ports the eleven source files of WORLD to C#. Fundamental frequency estimation is provided by Dio, Harvest and StoneMask, spectral envelope estimation by CheapTrick, aperiodicity estimation by D4C, waveform generation by Synthesis and by a sequential real-time synthesizer, and low-dimensional representation by the spectral envelope codec. WAV input and output and the analysis parameter file format are ported as well.

The public surface is modern C#. Waveforms and parameters are passed as `ReadOnlySpan<double>` and `Span<double>`, options are `readonly struct` types with `init` accessors and a `Default` factory, and each algorithm is a static class. The pointer-based internals are not exposed.

All working memory is served by `WorldArena`, a bump allocator backed by `NativeMemory.AlignedAlloc` with 64-byte alignment. The arena is a chain of chunks, so growing it never invalidates a pointer that was already handed out. Scratch buffers are declared once as a `Layout` method on a dedicated type; a source generator reads that method's signature and emits both the size query and the binding call, so the reported size and the actual consumption cannot drift apart.

The original C++ is not vendored into this repository. The reference harness under `reference/` clones WORLD, builds it with MSVC on Windows or with GCC on Linux, and dumps the input and output of every stage as raw doubles. The test suite loads those dumps and compares them against the C# results.

---

## Requirements

| Item | Requirement |
|---|---|
| Runtime | .NET 8.0 or later |
| Processor | x64 and ARM64 on Windows and Linux. CI verifies each of them |
| OS | Windows, Linux or macOS supported by the runtime. CI covers Windows and Linux |
| SDK | .NET SDK 10.0, required only to build from source |
| Language | C# 14 or later, required only to build from source (`LangVersion` is set to `latest`) |
| Unsafe code | Required in the consuming project only when `WorldArena.FromNativeMemory` is used |
| Reference data | The MSVC C++ toolset on Windows or GCC on Linux, and Git. Required only to regenerate the golden data used by the tests |

---

## Installation

1. Add the package from [NuGet](https://www.nuget.org/packages/WorldNet).

   ```sh
   dotnet add package WorldNet
   ```

2. Create a `WorldArena` once and reuse it for every call. The arena grows on first use and performs no further allocation afterwards.

---

## Usage

The following program reads a monaural WAV file, estimates the F0 contour with Harvest, the spectral envelope with CheapTrick and the aperiodicity with D4C, and synthesizes a waveform from the three results.

```csharp
using WorldNet;

int length = WaveFile.GetLength("input.wav");
double[] x = new double[length];
WaveFile.Read("input.wav", x, out int fs, out _);

using WorldArena arena = new();

HarvestOption harvestOption = HarvestOption.Default;
int f0Length = Harvest.GetSamplesForHarvest(fs, x.Length, harvestOption.FramePeriod);
double[] temporalPositions = new double[f0Length];
double[] f0 = new double[f0Length];
Harvest.Estimate(x, fs, harvestOption, temporalPositions, f0, arena);

CheapTrickOption cheapTrickOption = CheapTrickOption.Create(fs);
int fftSize = cheapTrickOption.FftSize;
int spectrumLength = (fftSize / 2) + 1;
double[] spectrogram = new double[f0Length * spectrumLength];
CheapTrick.Estimate(x, fs, cheapTrickOption, temporalPositions, f0, spectrogram, arena);

double[] aperiodicity = new double[f0Length * spectrumLength];
D4C.Estimate(x, fs, D4COption.Default, temporalPositions, f0, fftSize, aperiodicity, arena);

int yLength = Synthesis.GetSamplesForSynthesis(fs, f0Length, harvestOption.FramePeriod);
double[] y = new double[yLength];
Synthesis.Synthesize(f0, spectrogram, aperiodicity, fftSize, harvestOption.FramePeriod, fs, y, arena);

WaveFile.Write("output.wav", y, fs);
```

The caller sizes every destination array. `Harvest.GetSamplesForHarvest` returns the number of frames, and `Synthesis.GetSamplesForSynthesis` returns the number of samples of the synthesized waveform. The spectrogram and the aperiodicity are flat arrays that hold one row of `fftSize / 2 + 1` values per frame. `Synthesis.Synthesize` takes the length of `y` as the length of the output. `WaveFile.Read` accepts only monaural PCM files whose format chunk is 16 bytes long and throws `InvalidDataException` for any other header.

---

## Features

### 1. Fundamental frequency estimation

Dio estimates the F0 contour from the periodicity of band-passed signals and returns both the temporal positions and the contour. Its `Speed` option enables the decimation path, which lowers the sampling rate before the search. Harvest estimates the contour from instantaneous frequency candidates and produces a more robust result at a higher cost. StoneMask refines an existing contour using instantaneous frequency and is normally applied to the output of Dio.

Dio, Harvest and StoneMask all reproduce the original bit for bit on the reference waveform, including the decimation path of Dio.

### 2. Spectral envelope and aperiodicity

CheapTrick estimates the spectral envelope with an F0-adaptive window and pitch-synchronous smoothing. The FFT size is derived from the sampling rate and the F0 floor through `CheapTrick.GetFftSize`, and `CheapTrick.GetF0Floor` returns the inverse relation.

D4C estimates band aperiodicity and includes the D4C LoveTrain stage. Against the MSVC reference its result agrees with the original to within one unit in the last place; the difference originates in `Math.Pow`, which is not required to be correctly rounded and does not always return the same value as the MSVC runtime.

### 3. Waveform synthesis

`Synthesis.Synthesize` generates the waveform from the F0 contour, the spectrogram and the aperiodicity in one call. `WorldSynthesizer` implements the sequential real-time synthesizer, which accepts parameter chunks through `AddParameters` and produces output through `Synthesize` while managing an internal ring buffer.

The real-time synthesizer reproduces the original bit for bit, except against the MSVC reference on Windows ARM64. Against the MSVC reference the batch synthesizer agrees to within 4 units in the last place of the peak amplitude, and so does the real-time synthesizer on Windows ARM64. The original squares the aperiodicity with `pow(x, 2.0)` and this port computes `x * x`. The MSVC runtime does not always round the two alike, and the overlap-add accumulation amplifies the difference.

### 4. Spectral envelope coding

The codec converts the spectral envelope and the aperiodicity into a low-dimensional representation on the mel scale and back. `Codec.GetNumberOfAperiodicities` returns the number of coefficients for a given sampling rate.

Coding the aperiodicity, coding the spectral envelope and decoding the spectral envelope reproduce the original bit for bit. Against the MSVC reference decoding the aperiodicity agrees to within one unit in the last place, again through `Math.Pow`.

### 5. Zero allocation and the arena

`WorldArena` hands out 64-byte aligned blocks from a chain of native chunks. `BeginScope` records the current position and restores it on disposal, so scratch taken inside a loop is released without freeing anything. `FromNativeMemory` wraps a buffer the caller already owns; such an arena never grows and throws when exhausted.

Every scratch requirement is declared once as a `Layout` method that is generic over an allocator. Running it with the measuring allocator yields the required byte count without touching memory, and running it with the arena allocator performs the actual binding. The source generator emits `GetRequiredArenaBytes` and `Bind` from that single signature.

The absence of managed allocation is measured, not asserted. `GC.GetAllocatedBytesForCurrentThread` reports a delta of zero bytes across the full analysis and synthesis pipeline and across the real-time synthesizer.

### 6. Numerical verification

The test suite compares against dumps produced by the original C++ built with MSVC on Windows and with GCC on Linux. The tests enforce the tolerances of the MSVC columns on both platforms. The ARM64 column was measured on a GitHub Actions `windows-11-arm` runner with the ARM64 build of MSVC. The GCC column records what was measured on Linux x64 with GCC 13.3 and glibc 2.39: the maximum difference is zero in every stage, including the stages that the tests allow to deviate from the MSVC reference.

| Stage | Agreement with MSVC on Windows x64 | Agreement with MSVC on Windows ARM64 | Agreement with GCC on Linux x64 |
|---|---|---|---|
| Ooura FFT, all four transforms, sizes 8 to 4096 | Bit-exact | Bit-exact | Bit-exact |
| matlabfunctions, common | Bit-exact | Bit-exact | Bit-exact |
| Dio, including the decimation path | Bit-exact | Bit-exact | Bit-exact |
| StoneMask, Harvest, CheapTrick | Bit-exact | Bit-exact | Bit-exact |
| WAV I/O, parameter file I/O | Bit-exact | Bit-exact | Bit-exact |
| Real-time synthesizer | Bit-exact | Within 4 ULP of the peak | Bit-exact |
| Coding of aperiodicity and spectral envelope, decoding of spectral envelope | Bit-exact | Bit-exact | Bit-exact |
| D4C, decoding of aperiodicity | Within 1 ULP | Within 1 ULP | Bit-exact |
| Batch synthesis | Within 4 ULP of the peak | Within 4 ULP of the peak | Bit-exact |

One ULP of the peak is the unit in the last place of the largest absolute sample of the reference waveform. It measures the error against the scale of the whole waveform, because a count of units in the last place at each sample is not meaningful where the waveform crosses zero. For batch synthesis the largest error measured against the MSVC reference is 0.25 ULP of the peak on Windows x64 and 1 ULP of the peak on Windows ARM64.

The transcendental functions are measured separately. `Math.Cos`, `Math.Sin`, `Math.Log`, `Math.Exp` and `Math.Log10` return exactly the same doubles as the MSVC runtime over the sampled ranges. `Math.Pow(10, v)` and the squaring `v * v` differ from the MSVC `pow` by at most one unit in the last place on fewer than one percent of the sampled inputs, and the remaining tolerances above follow from this. Against the GCC and glibc reference on Linux, the same measurements show no difference for any of these functions. The tests that run these measurements pass on Windows ARM64 as well. Replacing the squaring in both synthesizers with `Math.Pow(v, 2.0)`, called with an exponent that is not a constant, makes every test pass on Windows ARM64, including the bit-exact real-time test, so the squaring is the only source of the differences there.

Beyond equivalence, the suite covers degenerate input such as silence, direct current and white noise, extremely short input, determinism across repeated runs, thread safety with one arena per thread, operation on a caller-supplied arena, and full release of the arena after the pipeline. The suite contains 365 tests, and all of them pass when the reference data is available. CI also runs the suite on Linux with AVX2 disabled and with all hardware intrinsics disabled, which exercises the narrower vector paths and the scalar paths of the vectorized routines. CI also runs the suite on Linux ARM64 against reference data generated there with GCC, where the routines written with x86 intrinsics take their scalar paths, and on Windows ARM64 against reference data generated there with MSVC.

### 7. Performance

The first two tables compare the original C++ compiled with MSVC at `/O2` against this port published with Native AOT. Both are compiled ahead of time, so the comparison is like for like. Each table measures every build back to back in one session, so the ratio is the meaningful quantity while the absolute values move with the machine.

The first table is a fixed record taken on a dedicated workstation before the vectorization described below. It is not regenerated.

Intel Core i7-1360P under Windows 11, sample application published for an `x86-64-v3` baseline, best of 12 runs in milliseconds, analysing the 22050 Hz reference waveform of 17500 samples with a 5 ms frame period.

| Stage | C++ with MSVC | This port with Native AOT | Ratio |
|---|---:|---:|---:|
| Dio | 9.81 | 3.86 | 2.54x |
| StoneMask | 9.40 | 3.03 | 3.10x |
| CheapTrick | 7.84 | 8.66 | 0.91x |
| D4C | 33.28 | 29.00 | 1.15x |
| Synthesis | 9.78 | 8.98 | 1.09x |
| Harvest | 161.49 | 122.36 | 1.32x |

The second table is regenerated by the benchmark workflow on a GitHub Actions runner. The hardware differs from the workstation, which makes it an independent check that the ratios do not depend on one particular machine. It also lists this port running on the just-in-time compiler with its default settings.

<!-- BENCHMARK:CI:BEGIN -->

Measured by CI on a GitHub Actions `windows-latest` runner with AMD EPYC 7763 64-Core Processor. Figures are the best of 20 runs in milliseconds, analysing the 22050 Hz reference waveform of 17500 samples with a 5 ms frame period. Every run of the port is a fresh process, so the just-in-time column includes the cost of compiling the code and of running it before tiered compilation has optimized it. All builds run back to back in the same job, so the ratio is the stable quantity; the absolute values move with the shared runner. Recorded on 2026-10-06 from commit `7a887af`.

| Stage | C++ with MSVC | This port with Native AOT | Ratio | This port with JIT | Ratio |
|---|---:|---:|---:|---:|---:|
| Dio | 8.71 | 2.67 | 3.26x | 2.65 | 3.29x |
| StoneMask | 9.54 | 3.03 | 3.15x | 2.89 | 3.30x |
| CheapTrick | 8.95 | 6.63 | 1.35x | 6.33 | 1.41x |
| D4C | 39.31 | 29.07 | 1.35x | 27.67 | 1.42x |
| Synthesis | 10.98 | 7.57 | 1.45x | 7.29 | 1.51x |
| Harvest | 151.39 | 56.85 | 2.66x | 56.06 | 2.70x |

<!-- BENCHMARK:CI:END -->

A ratio above 1.00 means this port is faster than the original C++.

The following table was measured on Linux x64 against the original C++ built with GCC 13.3 at `-O2`, on a virtual machine with an Intel Xeon at 2.10 GHz and four cores. Each figure is the best of 25 runs in milliseconds. Every run is a fresh process that analyses the same 22050 Hz reference waveform of 17500 samples with a 5 ms frame period, so the just-in-time column includes the cost of compiling the code and of running it before tiered compilation has optimized it. It is a fixed record and is not regenerated.

| Stage | C++ with GCC | This port with Native AOT | Ratio | This port with JIT | Ratio |
|---|---:|---:|---:|---:|---:|
| Dio | 5.74 | 2.91 | 1.97x | 2.97 | 1.93x |
| StoneMask | 3.59 | 2.92 | 1.23x | 3.16 | 1.14x |
| CheapTrick | 9.07 | 5.52 | 1.64x | 5.60 | 1.62x |
| D4C | 33.41 | 22.93 | 1.46x | 23.72 | 1.41x |
| Synthesis | 8.39 | 6.74 | 1.24x | 6.97 | 1.20x |
| Harvest | 128.46 | 56.21 | 2.29x | 55.69 | 2.31x |

The routines that contain loops, together with the routines of the Ooura FFT, are marked with `MethodImplOptions.AggressiveOptimization`, so that they are optimized at the first call instead of starting in the unoptimized tier. Before they were marked, the same measurement took 126.67 ms from Dio to Synthesis and 141.68 ms for Harvest, against 55.65 ms and 123.50 ms after they were marked. Once a process has warmed up, the speed does not depend on the mark: the best of 100 repeats within one process took 54.35 ms from Dio to Synthesis before the routines were marked and 54.20 ms after.

Several routines are vectorized in a way that leaves every output bit unchanged. On x64 processors with AVX2, the Ooura FFT evaluates two complex columns, or two independent blocks, in each 256-bit vector. Only the order in which independent butterflies are evaluated changes, no value takes a different sequence of operations, and no fused multiply-add is used. The pseudo-random generator of the original is a serial recurrence. Here it generates eight segments of the same sequence in parallel from precomputed starting states and returns the values in the original order. The refinement stage of Harvest transforms the waveform once for each distinct window length of a frame instead of once for each candidate, and the zero-crossing detection shared by Dio and Harvest compares several samples per instruction. In the table above, the JIT column read 1.26x for Dio, 1.03x for StoneMask, 1.02x for CheapTrick, 1.12x for D4C, 0.93x for Synthesis and 1.01x for Harvest before these changes. The tests that compare every stage with the reference pass unchanged. Processors without AVX2 run the scalar code and produce the same values. ARM64 uses that code too, and its speed against the original has not been measured.

---

## API Reference

### Analysis

| Member | Description |
|---|---|
| `Dio.GetSamplesForDio(fs, xLength, framePeriod)` | Returns the number of frames the contour will contain. |
| `Dio.Estimate(x, fs, option, temporalPositions, f0, arena)` | Estimates the F0 contour and the temporal positions. |
| `Harvest.GetSamplesForHarvest(fs, xLength, framePeriod)` | Returns the number of frames the contour will contain. |
| `Harvest.Estimate(x, fs, option, temporalPositions, f0, arena)` | Estimates the F0 contour and the temporal positions. |
| `StoneMask.Refine(x, fs, temporalPositions, f0, refinedF0, arena)` | Refines an existing contour using instantaneous frequency. |
| `CheapTrick.GetFftSize(fs, f0Floor)` | Returns the FFT size implied by the sampling rate and the F0 floor. |
| `CheapTrick.GetF0Floor(fs, fftSize)` | Returns the F0 floor implied by the sampling rate and the FFT size. |
| `CheapTrick.Estimate(x, fs, option, temporalPositions, f0, spectrogram, arena)` | Estimates the spectral envelope. |
| `D4C.Estimate(x, fs, option, temporalPositions, f0, fftSize, aperiodicity, arena)` | Estimates the band aperiodicity. |

### Synthesis

| Member | Description |
|---|---|
| `Synthesis.GetSamplesForSynthesis(fs, f0Length, framePeriod)` | Returns the number of samples the waveform will contain. |
| `Synthesis.Synthesize(f0, spectrogram, aperiodicity, fftSize, framePeriod, fs, y, arena)` | Generates the whole waveform in one call. |
| `new WorldSynthesizer(arena, fs, framePeriod, fftSize, bufferSize, numberOfPointers, maxFramesPerAdd)` | Creates the sequential real-time synthesizer. |
| `WorldSynthesizer.AddParameters(f0, spectrogram, aperiodicity)` | Queues one chunk of parameters and reports whether it was accepted. |
| `WorldSynthesizer.Synthesize()` | Produces the next block and reports whether output is available. |
| `WorldSynthesizer.Buffer` | Exposes the current output block. |
| `WorldSynthesizer.IsLocked` | Reports whether the internal buffer is full. |
| `WorldSynthesizer.Refresh()` | Clears the queued parameters and the internal state. |

### Coding

| Member | Description |
|---|---|
| `Codec.GetNumberOfAperiodicities(fs)` | Returns the number of aperiodicity coefficients. |
| `Codec.CodeAperiodicity(aperiodicity, f0Length, fs, fftSize, codedAperiodicity, arena)` | Reduces the aperiodicity to coefficients. |
| `Codec.DecodeAperiodicity(codedAperiodicity, f0Length, fs, fftSize, aperiodicity, arena)` | Restores the aperiodicity from coefficients. |
| `Codec.CodeSpectralEnvelope(spectrogram, f0Length, fs, fftSize, numberOfDimensions, codedSpectralEnvelope, arena)` | Reduces the spectral envelope on the mel scale. |
| `Codec.DecodeSpectralEnvelope(codedSpectralEnvelope, f0Length, fs, fftSize, numberOfDimensions, spectrogram, arena)` | Restores the spectral envelope. |

### Memory

| Member | Description |
|---|---|
| `new WorldArena()` | Creates an arena that grows on demand. |
| `new WorldArena(initialCapacityInBytes)` | Creates an arena with an initial chunk. |
| `WorldArena.FromNativeMemory(buffer, capacityInBytes)` | Wraps a caller-owned 64-byte aligned buffer. The arena cannot grow. |
| `WorldArena.GetReservedBytes(count, elementSize)` | Returns the aligned reservation for one allocation. |
| `WorldArena.EnsureCapacity(byteCount)` | Adds a chunk so that at least this many bytes are free. |
| `WorldArena.BeginScope()` | Records the position; disposing the returned scope restores it. |
| `WorldArena.Reset()` | Releases every allocation while keeping the chunks. |
| `WorldArena.Capacity`, `WorldArena.Used` | Report the totals across all chunks. |

### File I/O

| Member | Description |
|---|---|
| `WaveFile.GetLength(path)` | Returns the number of samples in the file. |
| `WaveFile.Read(path, destination, out sampleRate, out bitDepth)` | Reads a WAV file into a span. |
| `WaveFile.Write(path, x, sampleRate)` | Writes a 16-bit WAV file. |
| `ParameterFile.WriteF0`, `ParameterFile.ReadF0` | Write and read the F0 contour in the analysis parameter format. |
| `ParameterFile.WriteSpectralEnvelope`, `ParameterFile.ReadSpectralEnvelope` | Write and read the spectral envelope. |
| `ParameterFile.WriteAperiodicity`, `ParameterFile.ReadAperiodicity` | Write and read the aperiodicity. |
| `ParameterFile.GetHeaderInformation(path, parameter)` | Reads one header field from a parameter file. |

### Option defaults

| Option | Property | Default | Description |
|---|---|---|---|
| `DioOption` | `F0Floor` | 71.0 | Lower bound of the search range in hertz. |
| `DioOption` | `F0Ceil` | 800.0 | Upper bound of the search range in hertz. |
| `DioOption` | `ChannelsInOctave` | 2.0 | Number of band-pass filters per octave. |
| `DioOption` | `FramePeriod` | 5.0 | Frame shift in milliseconds. |
| `DioOption` | `Speed` | 1 | Decimation ratio from 1 to 12. Higher values are faster and coarser. |
| `DioOption` | `AllowedRange` | 0.1 | Threshold used when fixing the contour. |
| `HarvestOption` | `F0Floor` | 71.0 | Lower bound of the search range in hertz. |
| `HarvestOption` | `F0Ceil` | 800.0 | Upper bound of the search range in hertz. |
| `HarvestOption` | `FramePeriod` | 5.0 | Frame shift in milliseconds. |
| `CheapTrickOption` | `Q1` | -0.15 | Parameter of the spectral recovery. |
| `CheapTrickOption` | `F0Floor` | 71.0 | F0 floor that determines the FFT size. |
| `CheapTrickOption` | `FftSize` | derived | Computed from the sampling rate by `CheapTrickOption.Create`. |
| `D4COption` | `Threshold` | 0.85 | Threshold of the D4C LoveTrain stage. |

`DioOption.Default`, `HarvestOption.Default` and `D4COption.Default` return the values above. `CheapTrickOption.Create(fs)` is used instead because the FFT size depends on the sampling rate. All four are `readonly struct` types with `init` accessors, so a modified copy is produced with a `with` expression.

---

## Building from source

Clone the repository and build it with the .NET SDK 10.0 by running the following command in its root directory.

```sh
dotnet build WorldNet.slnx -c Release
```

To use the checkout instead of the package, add `WorldNet/WorldNet.csproj` as a project reference.

1. To run the test suite, generate the reference data first by executing `reference/build.bat` on Windows or `reference/build.sh` on Linux. Either script clones WORLD, builds it and writes the dumps to `reference/data`.
2. To produce a Native AOT binary of the sample application, run `publish-aot.bat`.

---

## Limitations

- Against the MSVC reference, the batch synthesizer agrees with the original to within 4 units in the last place of the peak amplitude rather than exactly, and D4C and the decoding of aperiodicity agree to within one unit in the last place. On Windows ARM64 the real-time synthesizer agrees to within the same 4 units. All of these follow from `pow`, which neither the .NET runtime nor the MSVC runtime is required to round correctly.
- Bit-exactness has been verified against WORLD compiled with MSVC on Windows x64 and against WORLD compiled with GCC 13.3 on Linux x64 with glibc 2.39. On Linux ARM64 and on Windows ARM64 the tests pass against WORLD compiled with GCC and with MSVC respectively, within the tolerances of the table above. In every case the tests pass on .NET 8 and on .NET 10. Other compilers, other runtimes and other architectures may round the transcendental functions differently, and the agreement above is not claimed for them.
- macOS is not covered by CI. A one-off run on macOS 26 on ARM64 with Apple clang 21 built the library and passed 318 of the 336 tests. The failures were the comparisons with the reference and the identity test of `Math.SinCos` on .NET 10. Apple clang merges the `sin` and `cos` of one argument into a call of `__sincos_stret`, which returns different values from separate calls. With that merge disabled in the reference build and the library calling `Math.Sin` and `Math.Cos` separately, only the D4C comparison failed, and it passed once the compiler's handling of `pow` was disabled as well. Bit-exactness against the original is therefore not claimed on macOS.
- The vector code of the FFT and of the pseudo-random generator runs only on x64 with AVX2. Other processors, including ARM64, run the scalar code and return the same values. Their speed against the original has not been measured.
- `WorldArena` is not thread-safe. Concurrent analysis requires one arena per thread, which the test suite exercises.
- An arena created by `FromNativeMemory` cannot grow. Run the same calls once with a growing arena, read `Capacity`, and pass a buffer of at least that many bytes plus 64 bytes for the arena header. `Used` cannot serve this purpose, because each call releases its scratch memory and `Used` is zero once the calls return. The size depends on the length and the sampling rate of the input and on the options.
- Running the comparison tests requires the reference data. Without `reference/data` those tests are skipped, and the remaining tests do not depend on it.
- The sample application under `WorldNet.Examples` reads and writes files and therefore allocates managed memory. The zero-allocation guarantee applies to the library.

---

## Notes

- Processing cost: Harvest is by far the most expensive stage, followed by D4C. Dio with a higher `Speed` is the cheapest way to obtain a contour. The figures in the performance table are indicative of one machine and vary between runs. `WorldNet.Examples bench <input.wav>` reports the per-stage timings on your own hardware, and the reference harness prints the same figures for the original C++ when `WORLD_BENCH_ONLY` is set.
- Arena reuse: the arena grows during the first call and performs no further allocation. Creating a new arena for every call defeats the purpose and reintroduces native allocation.
- Determinism: the pipeline produces identical output across repeated runs. The pseudo-random generator used by D4C and by the synthesizer is the xorshift generator of the original, reseeded to the same state, and it reproduces the original sequence exactly. With AVX2 it computes eight segments of the sequence in parallel and still returns the values in the original order. Without AVX2 it runs the original recurrence.
- Scratch layout: a type marked with `[ScratchLayout]` must expose a `Layout` method generic over `IScratchAllocator`. The generator emits `GetRequiredArenaBytes` and `Bind` with a matching parameter list, and skips whichever of the two the type already declares.
- Native AOT: the library sets `IsAotCompatible`, which enables the trim, single-file and AOT analyzers. `publish-aot.bat` publishes the sample application for `win-x64` and requires the MSVC toolset for the native linker.
- Just-in-time compilation: the routines that contain loops and the routines of the Ooura FFT carry `MethodImplOptions.AggressiveOptimization`, which skips the unoptimized first tier and compiles them with full optimization at the first call. The first call in a process therefore pays the compilation time. Once the process has warmed up, the speed is the same with and without the mark.
- Sine and cosine: the `net10.0` build computes the minimum phase spectrum with `Math.SinCos`, and the `net8.0` build calls `Math.Sin` and `Math.Cos` separately. On .NET 8 under Windows, `Math.SinCos` differs from the separate calls by one unit in the last place, which would break the agreement with the original.
- Regenerating reference data: `reference/build.bat` on Windows and `reference/build.sh` on Linux clone WORLD into `reference/world-src`, build it, and write the dumps. Both directories are excluded from version control.
- Network access: the library opens no network connection and sends no data.

---

## Reporting issues

Report problems on GitHub [Issues](https://github.com/routersys/WorldNet/issues). The library does not send any report automatically.

Please include the following in the report.

| Item | Content |
|---|---|
| Environment | The version of WorldNet, the version of the .NET SDK or runtime, the operating system and the processor architecture |
| Input | The sampling rate and the length of the waveform, and the stage in which the problem appears |
| Steps | The calls in the order they were made, including every option that differs from its default |
| Result | The expected result and the actual result. For a numerical difference, the stage, the index and both values |
| Exception | The type, the message and the stack trace, unmodified |

---

## Disclaimer

This library is published under the MIT License.

This software is provided "as is", without warranty of any kind, express or implied, including but not limited to the warranties of merchantability, fitness for a particular purpose and noninfringement.

The author accepts no liability for any damage arising from the use of or the inability to use this library. Use it at your own risk.

---

## Third-Party Licenses

WorldNet is a derivative work of the software below. The full license texts are stored in the repository under [`.github/LICENSE/WORLD.txt`](https://github.com/routersys/WorldNet/blob/main/.github/LICENSE/WORLD.txt) and [`.github/LICENSE/OouraFFT.txt`](https://github.com/routersys/WorldNet/blob/main/.github/LICENSE/OouraFFT.txt), and in the NuGet package under `THIRD-PARTY-NOTICES/`.

WORLD is distributed under the modified BSD license, which requires that redistributions of source code retain its copyright notice, the list of conditions and the disclaimer. Those files carry that text unmodified. No third-party source code is vendored into this repository, and the reference harness downloads WORLD on demand.

| Software | Purpose | License | Copyright |
|---|---|---|---|
| [WORLD](https://github.com/mmorise/World) | Origin of every ported algorithm and of the reference implementation used for verification | Modified BSD License | Copyright (c) 2010 M. Morise |
| [Ooura FFT](https://www.kurims.kyoto-u.ac.jp/~ooura/fft.html) | Fast Fourier transform carried by WORLD and ported here | Free use permitted by the author | Copyright Takuya OOURA, 1996-2001 |

---

## License

[MIT License](https://github.com/routersys/WorldNet/blob/main/LICENSE.txt)
