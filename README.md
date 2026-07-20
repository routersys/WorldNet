# WorldNet

[![License](https://img.shields.io/badge/license-MIT-blue.svg)](#)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)](#)
[![Release](https://img.shields.io/github/v/release/routersys/WorldNet.svg)](https://github.com/routersys/WorldNet/releases)

English | [日本語](https://github.com/routersys/WorldNet/blob/main/README.ja.md)

---

A complete C# port of [WORLD](https://github.com/mmorise/World), the vocoder-based speech analysis, manipulation and synthesis system by M. Morise.
Every stage runs without a single managed allocation, so the garbage collector never observes the analysis or the synthesis path.
All scratch memory comes from a native arena, every hot routine is written with `unsafe` pointers, and the whole library is annotated for Native AOT.
Correctness is not asserted from reading the source: each stage is compared against golden data produced by the original C++ compiled with MSVC.

---

## Table of Contents

1. [Overview](#overview)
2. [Requirements](#requirements)
3. [Installation](#installation)
4. [Features](#features)
   - [1. Fundamental frequency estimation](#1-fundamental-frequency-estimation)
   - [2. Spectral envelope and aperiodicity](#2-spectral-envelope-and-aperiodicity)
   - [3. Waveform synthesis](#3-waveform-synthesis)
   - [4. Spectral envelope coding](#4-spectral-envelope-coding)
   - [5. Zero allocation and the arena](#5-zero-allocation-and-the-arena)
   - [6. Numerical verification](#6-numerical-verification)
   - [7. Performance](#7-performance)
5. [API Reference](#api-reference)
   - [Analysis](#analysis)
   - [Synthesis](#synthesis)
   - [Coding](#coding)
   - [Memory](#memory)
   - [File I/O](#file-io)
   - [Option defaults](#option-defaults)
6. [Limitations](#limitations)
7. [Notes](#notes)
8. [Disclaimer](#disclaimer)
9. [Third-Party Licenses](#third-party-licenses)
10. [License](#license)

---

## Overview

WorldNet ports the eleven source files of WORLD to C#. Fundamental frequency estimation is provided by Dio, Harvest and StoneMask, spectral envelope estimation by CheapTrick, aperiodicity estimation by D4C, waveform generation by Synthesis and by a sequential real-time synthesizer, and low-dimensional representation by the spectral envelope codec. WAV input and output and the analysis parameter file format are ported as well.

The public surface is modern C#. Waveforms and parameters are passed as `ReadOnlySpan<double>` and `Span<double>`, options are `readonly struct` types with `init` accessors and a `Default` factory, and each algorithm is a static class. The pointer-based internals are not exposed.

All working memory is served by `WorldArena`, a bump allocator backed by `NativeMemory.AlignedAlloc` with 64-byte alignment. The arena is a chain of chunks, so growing it never invalidates a pointer that was already handed out. Scratch buffers are declared once as a `Layout` method on a dedicated type; a source generator reads that method's signature and emits both the size query and the binding call, so the reported size and the actual consumption cannot drift apart.

The original C++ is not vendored into this repository. The reference harness under `reference/` clones WORLD, builds it with MSVC, and dumps the input and output of every stage as raw doubles. The test suite loads those dumps and compares them against the C# results.

---

## Requirements

| Item | Requirement |
|---|---|
| OS | Windows, Linux or macOS supported by .NET 10 |
| SDK | .NET SDK 10.0 |
| Language | C# 14 or later (`LangVersion` is set to `latest`) |
| Unsafe code | Required in the consuming project only when `WorldArena.FromNativeMemory` is used |
| Reference data | MSVC C++ toolset and Git, required only to regenerate the golden data used by the tests |

---

## Installation

1. Clone the repository and add `WorldNet/WorldNet.csproj` as a project reference, or build it and reference the resulting assembly.
2. Create a `WorldArena` once and reuse it for every call. The arena grows on first use and performs no further allocation afterwards.
3. To run the test suite, generate the reference data first by executing `reference/build.bat`, which clones WORLD, builds it with MSVC and writes the dumps to `reference/data`.
4. To produce a Native AOT binary of the sample application, run `publish-aot.bat`.

---

## Features

### 1. Fundamental frequency estimation

Dio estimates the F0 contour from the periodicity of band-passed signals and returns both the temporal positions and the contour. Its `Speed` option enables the decimation path, which lowers the sampling rate before the search. Harvest estimates the contour from instantaneous frequency candidates and produces a more robust result at a higher cost. StoneMask refines an existing contour using instantaneous frequency and is normally applied to the output of Dio.

Dio, Harvest and StoneMask all reproduce the original bit for bit on the reference waveform, including the decimation path of Dio.

### 2. Spectral envelope and aperiodicity

CheapTrick estimates the spectral envelope with an F0-adaptive window and pitch-synchronous smoothing. The FFT size is derived from the sampling rate and the F0 floor through `CheapTrick.GetFftSize`, and `CheapTrick.GetF0Floor` returns the inverse relation.

D4C estimates band aperiodicity and includes the D4C LoveTrain stage. Its result agrees with the original to within one unit in the last place; the difference originates in `Math.Pow`, which is not required to be correctly rounded and does not always return the same value as the MSVC runtime.

### 3. Waveform synthesis

`Synthesis.Synthesize` generates the waveform from the F0 contour, the spectrogram and the aperiodicity in one call. `WorldSynthesizer` implements the sequential real-time synthesizer, which accepts parameter chunks through `AddParameters` and produces output through `Synthesize` while managing an internal ring buffer.

The real-time synthesizer reproduces the original bit for bit. The batch synthesizer agrees to within 64 units in the last place, inherited from the same `Math.Pow` difference and amplified by the overlap-add accumulation.

### 4. Spectral envelope coding

The codec converts the spectral envelope and the aperiodicity into a low-dimensional representation on the mel scale and back. `Codec.GetNumberOfAperiodicities` returns the number of coefficients for a given sampling rate.

Coding the aperiodicity, coding the spectral envelope and decoding the spectral envelope reproduce the original bit for bit. Decoding the aperiodicity agrees to within one unit in the last place, again through `Math.Pow`.

### 5. Zero allocation and the arena

`WorldArena` hands out 64-byte aligned blocks from a chain of native chunks. `BeginScope` records the current position and restores it on disposal, so scratch taken inside a loop is released without freeing anything. `FromNativeMemory` wraps a buffer the caller already owns; such an arena never grows and throws when exhausted.

Every scratch requirement is declared once as a `Layout` method that is generic over an allocator. Running it with the measuring allocator yields the required byte count without touching memory, and running it with the arena allocator performs the actual binding. The source generator emits `GetRequiredArenaBytes` and `Bind` from that single signature.

The absence of managed allocation is measured, not asserted. `GC.GetAllocatedBytesForCurrentThread` reports a delta of zero bytes across the full analysis and synthesis pipeline and across the real-time synthesizer.

### 6. Numerical verification

The test suite compares against dumps produced by the original C++ built with MSVC. The following table records the agreement that the tests enforce.

| Stage | Agreement |
|---|---|
| Ooura FFT, all four transforms, sizes 8 to 4096 | Bit-exact |
| matlabfunctions, common | Bit-exact |
| Dio, including the decimation path | Bit-exact |
| StoneMask, Harvest, CheapTrick | Bit-exact |
| Real-time synthesizer, WAV I/O, parameter file I/O | Bit-exact |
| Coding of aperiodicity and spectral envelope, decoding of spectral envelope | Bit-exact |
| D4C, decoding of aperiodicity | Within 1 ULP |
| Batch synthesis | Within 64 ULP |

The transcendental functions are measured separately. `Math.Cos`, `Math.Sin`, `Math.Log`, `Math.Exp` and `Math.Log10` return exactly the same doubles as the MSVC runtime over the sampled ranges. `Math.Pow(10, v)` and the squaring `v * v` differ from the MSVC `pow` by at most one unit in the last place on fewer than one percent of the sampled inputs, and the remaining tolerances above follow from this.

Beyond equivalence, the suite covers degenerate input such as silence, direct current and white noise, extremely short input, determinism across repeated runs, thread safety with one arena per thread, operation on a caller-supplied arena, and full release of the arena after the pipeline. The suite contains 330 tests and all of them pass.

### 7. Performance

The table compares the original C++ compiled with MSVC at `/O2` against this port published with Native AOT. Both are compiled ahead of time, so the comparison is like for like. The block below is regenerated by the benchmark workflow, which builds and measures both in a single job so that neither side gets a different machine.

<!-- BENCHMARK:BEGIN -->

Measured outside CI on an Intel Core i7-1360P under Windows 11, with the sample application published for an `x86-64-v3` baseline. Both builds were measured back to back in one session, so the ratio is the meaningful quantity; repeated sessions move each ratio by roughly ten percent. Figures are the best of 12 runs in milliseconds, analysing the 22050 Hz reference waveform of 17500 samples with a 5 ms frame period.

| Stage | C++ with MSVC | This port with Native AOT | Ratio |
|---|---:|---:|---:|
| Dio | 16.75 | 7.42 | 2.26x |
| StoneMask | 16.89 | 5.73 | 2.95x |
| CheapTrick | 14.09 | 16.19 | 0.87x |
| D4C | 59.28 | 58.15 | 1.02x |
| Synthesis | 17.25 | 15.81 | 1.09x |
| Harvest | 277.26 | 209.41 | 1.32x |

A ratio above 1.00 means this port is faster than the original C++.

<!-- BENCHMARK:END -->

Figures obtained through the just-in-time compiler are deliberately absent. The benchmark repeats each stage only twice, which is not enough for tiered compilation to settle. Disabling tiering with `DOTNET_TieredCompilation=0` brings the just-in-time figures back in line with the Native AOT column, which shows that the gap is a warm-up artefact of the measurement rather than a property of the port.

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
| `Codec.CodeAperiodicity(aperiodicity, f0Length, fs, fftSize, codedAperiodicity)` | Reduces the aperiodicity to coefficients. |
| `Codec.DecodeAperiodicity(codedAperiodicity, f0Length, fs, fftSize, aperiodicity)` | Restores the aperiodicity from coefficients. |
| `Codec.CodeSpectralEnvelope(spectrogram, f0Length, fs, fftSize, dimensions, coded)` | Reduces the spectral envelope on the mel scale. |
| `Codec.DecodeSpectralEnvelope(coded, f0Length, fs, fftSize, dimensions, spectrogram)` | Restores the spectral envelope. |

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

## Limitations

- The batch synthesizer agrees with the original to within 64 units in the last place rather than exactly. D4C and the decoding of aperiodicity agree to within one unit in the last place. Both follow from `Math.Pow`, which neither the .NET runtime nor the MSVC runtime is required to round correctly.
- Bit-exactness has been verified against WORLD compiled with MSVC on Windows x64. Other compilers, other runtimes and other architectures may round the transcendental functions differently, and the agreement above is not claimed for them.
- `WorldArena` is not thread-safe. Concurrent analysis requires one arena per thread, which the test suite exercises.
- An arena created by `FromNativeMemory` cannot grow. Determine the size by running once with a growing arena and reading `Used`, or by summing the `GetRequiredArenaBytes` of the stages you invoke.
- Running the comparison tests requires the reference data. Without `reference/data` those tests cannot execute.
- The sample application under `WorldNet.Examples` reads and writes files and therefore allocates managed memory. The zero-allocation guarantee applies to the library.

---

## Notes

- Processing cost: Harvest is by far the most expensive stage, followed by D4C. Dio with a higher `Speed` is the cheapest way to obtain a contour. The figures in the performance table are indicative of one machine and vary between runs. `WorldNet.Examples bench <input.wav>` reports the per-stage timings on your own hardware, and the reference harness prints the same figures for the original C++ when `WORLD_BENCH_ONLY` is set.
- Arena reuse: the arena grows during the first call and performs no further allocation. Creating a new arena for every call defeats the purpose and reintroduces native allocation.
- Determinism: the pipeline produces identical output across repeated runs. The pseudo-random generator used by D4C and by the synthesizer is the xorshift generator of the original, reseeded to the same state, and it reproduces the original sequence and its final state exactly.
- Scratch layout: a type marked with `[ScratchLayout]` must expose a `Layout` method generic over `IScratchAllocator`. The generator emits `GetRequiredArenaBytes` and `Bind` with a matching parameter list, and skips whichever of the two the type already declares.
- Native AOT: the library sets `IsAotCompatible`, which enables the trim, single-file and AOT analyzers. `publish-aot.bat` publishes the sample application for `win-x64` and requires the MSVC toolset for the native linker.
- Regenerating reference data: `reference/build.bat` clones WORLD into `reference/world-src`, builds it, and writes the dumps. Both directories are excluded from version control.

---

## Disclaimer

This library is published under the MIT License.

This software is provided "as is", without warranty of any kind, express or implied, including but not limited to the warranties of merchantability, fitness for a particular purpose and noninfringement.

The author accepts no liability for any damage arising from the use of or the inability to use this library. Use it at your own risk.

---

## Third-Party Licenses

WorldNet is a derivative work of the software below. The full license texts are stored in the repository under [`.github/LICENSE/WORLD.txt`](https://github.com/routersys/WorldNet/blob/main/.github/LICENSE/WORLD.txt) and [`.github/LICENSE/OouraFFT.txt`](https://github.com/routersys/WorldNet/blob/main/.github/LICENSE/OouraFFT.txt).

WORLD is distributed under the modified BSD license, which requires that redistributions of source code retain its copyright notice, the list of conditions and the disclaimer. Those files carry that text unmodified. No third-party source code is vendored into this repository, and the reference harness downloads WORLD on demand.

| Software | Purpose | License | Copyright |
|---|---|---|---|
| [WORLD](https://github.com/mmorise/World) | Origin of every ported algorithm and of the reference implementation used for verification | Modified BSD License | Copyright (c) 2010 M. Morise |
| [Ooura FFT](https://www.kurims.kyoto-u.ac.jp/~ooura/fft.html) | Fast Fourier transform carried by WORLD and ported here | Free use permitted by the author | Copyright Takuya OOURA, 1996-2001 |

---

## License

[MIT License](https://github.com/routersys/WorldNet/blob/main/LICENSE.txt)
