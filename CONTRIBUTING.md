# Contributing to WorldNet

English | [日本語](CONTRIBUTING.ja.md)

Thank you for your interest in contributing to WorldNet.

WorldNet is a C# port of [WORLD](https://github.com/mmorise/World). It is verified against the original, and its analysis and synthesis routines perform no managed allocation. Please read the [README](README.md) first: it states what the library guarantees and, just as importantly, what it does not.

---

## Table of Contents

1. [How to Contribute](#how-to-contribute)
2. [Getting Started](#getting-started)
3. [Issues](#issues)
4. [Pull Requests](#pull-requests)
5. [Verification](#verification)
6. [Code Conventions](#code-conventions)
7. [Commit Conventions](#commit-conventions)
8. [Releasing](#releasing)
9. [Code of Conduct](#code-of-conduct)

---

## How to Contribute

Typo fixes, documentation corrections and trivial bug fixes can go straight to a pull request.

Open an issue first for any of the following, so that the intended behavior and scope are agreed before implementation. If you are unsure which side your change falls on, open an issue.

- A larger bug fix
- A new feature
- A change to the agreement with the original WORLD
- A new public API, or a change to an existing one
- A change that touches the zero-allocation contract
- Support for a new platform or target framework

Report a vulnerability privately as the [security policy](SECURITY.md) describes, never in an issue.

Issues and pull requests are welcome in English or Japanese.

---

## Getting Started

### Environment

| Item | Requirement |
|---|---|
| SDK | .NET SDK 10.0 |
| Runtime | .NET 8.0 and .NET 10.0, because the test project targets both |
| Reference data | The MSVC C++ toolset on Windows or GCC on Linux, and Git. Required only to regenerate the data that the comparison tests read |

### Source

Fork the repository and clone your fork. Branch from `develop` for a feature, and from `main` for anything else.

### Build and test

```console
dotnet build WorldNet.slnx -c Release
dotnet test WorldNet.slnx -c Release
```

The comparison tests read the dumps under `reference/data`. Generate them with `reference\build.bat` on Windows or `reference/build.sh` on Linux. Either script clones WORLD, builds it and writes the dumps. Without the dumps, the comparison tests are skipped and the remaining tests do not depend on them.

The build treats warnings as errors and enforces code style. Do not work around either; fix the underlying issue.

---

## Issues

Open an issue from a template. Templates are available in English and Japanese for a bug report, a proposal, a question and a documentation problem.

For a bug report, include:

- the expected and actual behavior;
- the smallest code that reproduces it, with every option that differs from its default;
- the sampling rate and the number of samples of the input;
- the versions of WorldNet and of the .NET runtime, the operating system and the processor architecture;
- for a numerical difference, the stage, the index and both values, and whether the original WORLD gives the same result;
- any exception, with its type, message and stack trace unmodified.

Intermittent failures are worth reporting without a reliable reproduction. State how often the failure occurs and out of how many runs.

Attachments to an issue are visible to everyone. Attach audio only if you have the right to redistribute it. A synthetic signal that reproduces the problem is best.

---

## Pull Requests

A pull request that adds a feature targets `develop`. A bug fix and any other change targets `main`.

Keep each pull request to one logical change. Avoid unrelated refactoring, formatting changes and dependency updates; update a dependency only for a concrete reason, such as a required feature, a bug fix, a security fix or a compatibility requirement.

Follow the implementation pattern already established in the code you are changing. If existing implementations disagree, verify the intended behavior against the README and the tests instead of copying either one mechanically.

Take particular care when changing:

- the public API;
- numerical results, and the agreement with the original WORLD;
- the arena, the lifetime of its memory, and anything that allocates;
- the vectorized routines of the FFT and of the pseudo-random generator, which take a different code path on each processor;
- disposal and failure handling.

If a change alters what the library guarantees or documents, update [README.md](README.md) and [README.ja.md](README.ja.md) in the same pull request.

Do not include a version bump. The developer updates the version and cuts the release.

The developer reviews the pull request and merges it with a merge commit, which keeps your commits in the history. The [commit conventions](#commit-conventions) apply to every commit in the pull request.

Contributions are accepted under the repository's MIT [LICENSE](LICENSE.txt). By opening a pull request you agree that your contribution is licensed under those terms. If a change brings in third-party code or data, say so in the pull request and give its license.

---

## Verification

Report what you actually ran, in the pull request. Include the result of the build and of the tests, and say whether the comparison tests ran or were skipped for lack of the reference data. A skipped test is not a passing test.

Distinguish what you observed, what you derived from reading the code, and what you assume. A defect found by reading code is a real finding; say that it has not been reproduced.

### Numerical agreement

Do not make the agreement with the original WORLD worse than the table in the README section on numerical verification. A change that moves a figure in that table needs an issue first.

### Vector code paths

CI runs the whole test suite on Linux x64 normally, with `DOTNET_EnableAVX2=0`, and with `DOTNET_EnableHWIntrinsic=0`. It runs the suite normally and with `DOTNET_EnableHWIntrinsic=0` on Linux ARM64. For a change to a vectorized routine, run the tests with the same variables set.

### Performance

Do not claim an improvement from a single measurement.

- Compare the baseline and the candidate repeatedly, alternating between them, on the same machine under the same conditions.
- Report enough measurements to distinguish the change from the run-to-run variation.
- Run the correctness tests and the performance measurement separately.

`WorldNet.Examples bench <input.wav>` reports the time of each stage on your own hardware.

---

## Code Conventions

Follow the implementation patterns and the style already used in the code you are changing.

Do not add comments or commented-out code. Keep the comments and documentation comments that already exist in the code you modify.

Fix the build warnings that your change introduces.

---

## Commit Conventions

- Keep each commit to the smallest practical logical unit, and make sure every commit builds on its own.
- Keep implementation changes and the tests that verify them in separate commits.
- Commit subjects in this repository are written in Japanese, are 50 characters or fewer, and have no message body. If you cannot write the subject in Japanese, say so in the pull request.

---

## Releasing

The developer cuts the releases. The version follows semantic versioning and is decided from the difference in the public API, not from the size of the change: removing or narrowing anything a consumer could rely on is a major release, adding public API is a minor release, and anything else is a patch release.

---

## Code of Conduct

All contributors are expected to follow the repository's [Code of Conduct](CODE_OF_CONDUCT.md).
