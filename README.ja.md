# WorldNet

[![License](https://img.shields.io/badge/license-MIT-blue.svg)](#)
[![.NET](https://img.shields.io/badge/.NET-8.0%2B-purple.svg)](#)
[![Release](https://img.shields.io/github/v/release/routersys/WorldNet.svg)](https://github.com/routersys/WorldNet/releases)

[English](README.md) | 日本語

---

M. Morise氏による音声分析変換合成システム[WORLD](https://github.com/mmorise/World)を、C#へ完全に移植したライブラリです。
解析と合成のすべての処理がマネージドヒープを確保しないため、これらの処理がガベージコレクションを引き起こすことはありません。
作業領域はネイティブのアリーナから供給し、主要な演算はunsafeなポインターで記述しています。ライブラリ全体がNative AOTに対応しています。
正しさは、WindowsではMSVCで、LinuxではGCCでビルドした原典のC++が出力した参照データとの照合で確認しています。

---

## 目次

1. [概要](#概要)
2. [動作要件](#動作要件)
3. [インストール方法](#インストール方法)
4. [使い方](#使い方)
5. [主な機能](#主な機能)
   - [1. 基本周波数の推定](#1-基本周波数の推定)
   - [2. スペクトル包絡と非周期性指標](#2-スペクトル包絡と非周期性指標)
   - [3. 波形合成](#3-波形合成)
   - [4. スペクトル包絡の符号化](#4-スペクトル包絡の符号化)
   - [5. 無確保とアリーナ](#5-無確保とアリーナ)
   - [6. 数値検証](#6-数値検証)
   - [7. 性能](#7-性能)
6. [APIリファレンス](#apiリファレンス)
   - [解析](#解析)
   - [合成](#合成)
   - [符号化](#符号化)
   - [メモリ](#メモリ)
   - [ファイル入出力](#ファイル入出力)
   - [オプションの既定値](#オプションの既定値)
7. [ビルド方法](#ビルド方法)
8. [制限事項](#制限事項)
9. [注意事項](#注意事項)
10. [不具合の報告](#不具合の報告)
11. [免責事項](#免責事項)
12. [サードパーティライセンス](#サードパーティライセンス)
13. [ライセンス](#ライセンス)

---

## 概要

WorldNetは、WORLDの11個のソースファイルをC#へ移植したものです。基本周波数の推定はDioとHarvestとStoneMaskが、スペクトル包絡の推定はCheapTrickが、非周期性指標の推定はD4Cが担います。波形の生成は一括合成と実時間合成の2通りで、低次元表現への変換はコーデックが担います。WAVの入出力と、解析パラメーターのファイル形式も移植しています。

公開しているAPIは、モダンなC#で書かれています。波形とパラメーターは、`ReadOnlySpan<double>`と`Span<double>`で受け渡します。オプションは`init`アクセサーを持つ`readonly struct`で、既定値を作る静的メンバーを備えます。各アルゴリズムは静的クラスです。ポインターを使う内部の実装は、公開していません。

作業領域は、すべて`WorldArena`が供給します。`WorldArena`は、`NativeMemory.AlignedAlloc`で64バイト境界に確保した領域を、バンプアロケーターとして切り出します。領域は塊をつないで管理するため、容量が増えても、すでに渡したポインターは無効になりません。作業領域の要求は、専用の型の`Layout`メソッドに一度だけ書きます。ソースジェネレーターがそのシグネチャを読み、必要量の照会とバインドの両方を生成するため、報告する必要量と実際の消費量は食い違いません。

原典のC++は、このリポジトリに同梱していません。`reference`ディレクトリの参照ハーネスがWORLDを取得し、WindowsではMSVCで、LinuxではGCCでビルドして、各処理の入力と出力を倍精度のまま書き出します。テストはその出力を読み込み、C#の結果と照合します。

---

## 動作要件

| 項目 | 要件 |
|---|---|
| ランタイム | .NET 8.0以降 |
| プロセッサー | WindowsとLinuxのどちらでも、x64とARM64。いずれもCIで検証しています |
| OS | ランタイムが対応するWindows、Linux、macOS。CIはWindowsとLinuxを対象にしています |
| SDK | ソースからビルドする場合だけ、.NET SDK 10.0が必要です |
| 言語 | ソースからビルドする場合だけ、C# 14以降が必要です。`LangVersion`は`latest`を指定しています |
| unsafeコード | `WorldArena.FromNativeMemory`を使う場合だけ、利用するプロジェクトでunsafeコードを許可する設定が必要です |
| 参照データ | WindowsではMSVCのC++ツールセット、LinuxではGCC、およびGit。テストが使う参照データを再生成する場合だけ必要です |

---

## インストール方法

1. [NuGet](https://www.nuget.org/packages/WorldNet)からパッケージを追加してください。

   ```sh
   dotnet add package WorldNet
   ```

2. `WorldArena`は一度だけ生成し、以後の呼び出しで使い回してください。アリーナは初回の呼び出しで拡張し、以降は追加の確保を行いません。

---

## 使い方

次のプログラムは、モノラルのWAVファイルを読み込み、解析してから波形を合成します。F0の系列はHarvest、スペクトル包絡はCheapTrick、非周期性指標はD4Cで推定します。

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

出力先の配列の大きさは、呼び出し側が決めます。`Harvest.GetSamplesForHarvest`はフレームの個数を、`Synthesis.GetSamplesForSynthesis`は合成する波形のサンプルの個数を返します。スペクトログラムと非周期性指標は、1フレームにつき`fftSize / 2 + 1`個の値を並べた1次元の配列です。`Synthesis.Synthesize`は、`y`の長さを出力の長さとして扱います。`WaveFile.Read`が受け付けるのは、フォーマットチャンクが16バイトのモノラルPCMファイルだけで、それ以外のヘッダーでは`InvalidDataException`を投げます。

---

## 主な機能

### 1. 基本周波数の推定

Dioは、帯域通過フィルターを通した信号の周期性からF0の系列を推定し、時刻の系列と併せて返します。`Speed`オプションを上げると間引きを行い、探索の前にサンプリング周波数を下げます。Harvestは、瞬時周波数から得た候補をもとに系列を推定します。計算量と引き換えに、より頑健な結果を返します。StoneMaskは既存の系列を瞬時周波数で精密化するもので、通常はDioの出力に適用します。

DioとHarvestとStoneMaskは、参照波形で原典とビット単位で一致します。Dioの間引きを行う場合も一致します。

### 2. スペクトル包絡と非周期性指標

CheapTrickは、F0に適応した窓とピッチ同期の平滑化で、スペクトル包絡を推定します。FFTサイズはサンプリング周波数とF0の下限から決まり、`CheapTrick.GetFftSize`が求めます。逆に、サンプリング周波数とFFTサイズからF0の下限を求めるのが`CheapTrick.GetF0Floor`です。

D4Cは、帯域ごとの非周期性指標を推定します。D4C LoveTrainの処理も含みます。MSVCの参照に対する結果と原典との差は、1 ULP以内です。ULPは、ある浮動小数点数と、その次に表せる浮動小数点数との間隔です。差の原因は`Math.Pow`にあります。この関数は、正しく丸めることを要求されておらず、MSVCのランタイムライブラリと常に同じ値を返すとは限りません。

### 3. 波形合成

`Synthesis.Synthesize`は、F0の系列とスペクトログラムと非周期性指標から、波形を一度の呼び出しで生成します。`WorldSynthesizer`は実時間合成を実装しています。`AddParameters`でパラメーターの塊を受け取り、内部のリングバッファーを管理しながら、`Synthesize`で出力を生成します。

実時間合成は、Windows ARM64のMSVCの参照に対する場合を除いて、原典とビット単位で一致します。MSVCの参照に対する一括合成の差は、最大振幅の4 ULP以内です。Windows ARM64の実時間合成の差も、同じ範囲に収まります。原典は非周期性指標の二乗を`pow(x, 2.0)`で求め、本移植は`x * x`で求めます。MSVCのランタイムは、この2つを常に同じには丸めません。その差が、オーバーラップ加算の累積で拡大します。

### 4. スペクトル包絡の符号化

符号化は、スペクトル包絡と非周期性指標をメル尺度上の低次元表現へ変換し、また元の表現へ戻す処理です。`Codec.GetNumberOfAperiodicities`は、サンプリング周波数に対する係数の個数を返します。

非周期性指標の符号化と、スペクトル包絡の符号化および復号は、原典とビット単位で一致します。非周期性指標の復号だけは、MSVCの参照に対する差が1 ULP以内で、これも`Math.Pow`が原因です。

### 5. 無確保とアリーナ

`WorldArena`は、ネイティブの塊をつないだ領域から、64バイト境界の領域を切り出します。`BeginScope`は現在位置を記録し、破棄されるときにその位置へ戻ります。そのため、繰り返しの内側で確保した作業領域は、解放処理をしなくても元に戻ります。`FromNativeMemory`は、呼び出し側が所有する領域を包みます。このアリーナは拡張せず、容量が尽きると例外を投げます。

作業領域の要求は、アロケーターを型引数に取る`Layout`メソッドに、一度だけ書きます。測定用のアロケーターで実行すると、メモリに触れずに必要量が求まります。アリーナのアロケーターで実行すると、実際のバインドが行われます。ソースジェネレーターは、この一つのシグネチャから`GetRequiredArenaBytes`と`Bind`を生成します。

マネージドヒープを確保しないことは、主張ではなく測定で確かめています。`GC.GetAllocatedBytesForCurrentThread`の増分は、解析から合成までの全体でも、実時間合成でも0バイトです。

### 6. 数値検証

テストは、WindowsではMSVCで、LinuxではGCCでビルドした原典のC++の出力と照合します。許容の幅は、どちらのプラットフォームでも、MSVCの列のとおりに強制します。ARM64の列は、GitHub Actionsの`windows-11-arm`ランナーで、MSVCのARM64版を使って測った結果です。GCCの列は、Linux x64でGCC 13.3とglibc 2.39を使って測った結果です。MSVCに対して許容の幅を設けている処理を含め、すべての処理で最大の差が0でした。

| 対象 | Windows x64のMSVCとの一致 | Windows ARM64のMSVCとの一致 | Linux x64のGCCとの一致 |
|---|---|---|---|
| Ooura FFT、4種の変換、サイズ8から4096 | ビット単位で一致 | ビット単位で一致 | ビット単位で一致 |
| matlabfunctions、common | ビット単位で一致 | ビット単位で一致 | ビット単位で一致 |
| Dio、間引きを行う場合を含む | ビット単位で一致 | ビット単位で一致 | ビット単位で一致 |
| StoneMask、Harvest、CheapTrick | ビット単位で一致 | ビット単位で一致 | ビット単位で一致 |
| WAV入出力、パラメーターファイル入出力 | ビット単位で一致 | ビット単位で一致 | ビット単位で一致 |
| 実時間合成 | ビット単位で一致 | 最大振幅の4 ULP以内 | ビット単位で一致 |
| 非周期性指標の符号化、スペクトル包絡の符号化と復号 | ビット単位で一致 | ビット単位で一致 | ビット単位で一致 |
| D4C、非周期性指標の復号 | 1 ULP以内 | 1 ULP以内 | ビット単位で一致 |
| 一括合成 | 最大振幅の4 ULP以内 | 最大振幅の4 ULP以内 | ビット単位で一致 |

最大振幅の1 ULPとは、参照の波形で絶対値が最大のサンプルについて、最後の桁の1単位のことです。波形が0を横切る付近では、サンプルごとに最後の桁の単位を数えても意味がないため、波形全体の大きさを基準にして誤差を測ります。一括合成の最大の誤差を、MSVCの参照に対して測りました。Windows x64では最大振幅の0.25 ULP、Windows ARM64では最大振幅の1 ULPです。

超越関数は、個別に測定しています。`Math.Cos`と`Math.Sin`と`Math.Log`と`Math.Exp`と`Math.Log10`は、測定した範囲で、MSVCのランタイムライブラリと同一の倍精度値を返します。`Math.Pow(10, v)`と二乗の`v * v`は、MSVCの`pow`との差が最大1 ULPで、差が出るのは、測定した入力の1%未満です。上の許容範囲は、この測定から決まっています。LinuxのGCCとglibcの参照に対しては、同じ測定で、これらの関数のどれにも差がありませんでした。この測定を行うテストは、Windows ARM64でも通ります。両方の合成の二乗を、`Math.Pow(v, 2.0)`へ置き換えます。指数は、定数でない値で渡します。この置き換えで、Windows ARM64のすべてのテストが通ります。ビット単位の一致を求める実時間合成のテストも含みます。したがって、Windows ARM64での差の原因は二乗だけです。

等価性の確認に加えて、次の項目を試験しています。

- 無音、直流、白色雑音といった退化した入力
- 極端に短い入力
- 繰り返し実行したときの決定性
- スレッドごとにアリーナを分けた場合の安全性
- 呼び出し側が渡したアリーナでの動作
- 処理の終了後に、アリーナがすべて解放されること

テストは379件あり、参照データを用意した環境では、すべて通ります。CIでは、Linuxで、AVX2を無効にした場合と、ハードウェア命令をすべて無効にした場合にも試験を実行します。これにより、幅の狭いベクトル演算の経路と、スカラーの経路を通ります。LinuxのARM64でも、そこでGCCを使って生成した参照データに対して、128ビットの経路を使う場合と、ハードウェア命令をすべて無効にした場合の2通りで試験を実行します。Windows ARM64でも、そこでMSVCを使って生成した参照データに対して試験を実行します。

### 7. 性能

最初の2つの表では、MSVCで`/O2`を指定してビルドした原典のC++と、本移植をNative AOTで発行したものとを比べます。どちらも事前コンパイルなので、条件はそろっています。どの表も、すべてのビルドを同じセッションの中で続けて測っています。意味を持つ量は比で、絶対値はコンピューターによって変わります。

一つ目の表は、専用のコンピューターで、後述のベクトル化の前に取った固定の記録で、再生成しません。

Intel Core i7-1360Pを搭載したWindows 11で、実行例を`x86-64-v3`を基準の命令セットとして発行しました。12回実行した最小値を、ミリ秒で示します。サンプリング周波数22050Hz、17500サンプルの参照波形を、フレーム周期5ミリ秒で解析しています。

| 処理 | MSVCのC++ | 本移植のNativeAOT | 比 |
|---|---:|---:|---:|
| Dio | 9.81 | 3.86 | 2.54x |
| StoneMask | 9.40 | 3.03 | 3.10x |
| CheapTrick | 7.84 | 8.66 | 0.91x |
| D4C | 33.28 | 29.00 | 1.15x |
| Synthesis | 9.78 | 8.98 | 1.09x |
| Harvest | 161.49 | 122.36 | 1.32x |

二つ目の表は、GitHub Actionsのランナー上で、計測用のワークフローが再生成します。専用のコンピューターとは機種が異なるため、比が特定の機種に依存しないことを、独立して確かめる表でもあります。この表には、本移植をJITコンパイラーの既定の設定で動かした値も加えています。

<!-- BENCHMARK:CI:BEGIN -->

GitHub Actionsの`windows-latest`ランナー上で、CIが計測した値です。プロセッサーはAMD EPYC 7763 64-Core Processorです。20回実行した最小値を、ミリ秒で示します。サンプリング周波数22050Hz、17500サンプルの参照波形を、フレーム周期5ミリ秒で解析しています。本移植は毎回新しいプロセスで実行するので、JITの列には、コードのコンパイルと、階層型コンパイルが最適化する前のコードの実行が含まれます。すべてを同じジョブの中で続けて実行するので、安定する量は比で、絶対値は共有ランナーの状態によって変わります。計測日は2026-10-06、対象のコミットは`7a887af`です。

| 処理 | MSVCのC++ | 本移植のNativeAOT | 比 | 本移植のJIT | 比 |
|---|---:|---:|---:|---:|---:|
| Dio | 8.71 | 2.67 | 3.26x | 2.65 | 3.29x |
| StoneMask | 9.54 | 3.03 | 3.15x | 2.89 | 3.30x |
| CheapTrick | 8.95 | 6.63 | 1.35x | 6.33 | 1.41x |
| D4C | 39.31 | 29.07 | 1.35x | 27.67 | 1.42x |
| Synthesis | 10.98 | 7.57 | 1.45x | 7.29 | 1.51x |
| Harvest | 151.39 | 56.85 | 2.66x | 56.06 | 2.70x |

<!-- BENCHMARK:CI:END -->

比が1.00を超える処理は、本移植が原典のC++より速いことを示します。

次の表は、Linux x64で、GCC 13.3を使い`-O2`でビルドした原典のC++と比べて測りました。計測機は、Intel Xeon 2.10GHzと4コアを持つ仮想マシンです。単位はミリ秒で、25回実行した最小値です。毎回新しいプロセスで、サンプリング周波数22050Hz、17500サンプルの同じ参照波形を、フレーム周期5ミリ秒で解析しています。そのため、JITの列には、コードのコンパイルと、階層型コンパイルが最適化する前のコードの実行が含まれます。この表は固定の記録で、再生成しません。

| 処理 | GCCのC++ | 本移植のNativeAOT | 比 | 本移植のJIT | 比 |
|---|---:|---:|---:|---:|---:|
| Dio | 5.74 | 2.91 | 1.97x | 2.97 | 1.93x |
| StoneMask | 3.59 | 2.92 | 1.23x | 3.16 | 1.14x |
| CheapTrick | 9.07 | 5.52 | 1.64x | 5.60 | 1.62x |
| D4C | 33.41 | 22.93 | 1.46x | 23.72 | 1.41x |
| Synthesis | 8.39 | 6.74 | 1.24x | 6.97 | 1.20x |
| Harvest | 128.46 | 56.21 | 2.29x | 55.69 | 2.31x |

ループを含む処理とOoura FFTには`MethodImplOptions.AggressiveOptimization`を付けています。最適化されていない最初の階層から始めず、最初の呼び出しで最適化するためです。この指定を付ける前の同じ測定では、DioからSynthesisまでの合計が126.67ミリ秒、Harvestが141.68ミリ秒でした。指定を付けた後は、それぞれ55.65ミリ秒と123.50ミリ秒でした。プロセスが暖まった後の速さは、指定の有無で変わりません。1つのプロセスの中で100回繰り返した最小値は、DioからSynthesisまでの合計で、指定を付ける前が54.35ミリ秒、付けた後が54.20ミリ秒でした。

出力のビットを変えない範囲で、いくつかの処理をベクトル化しています。AVX2を持つx64のプロセッサーでは、Ooura FFTが、256ビットのベクトル1つで、複素数2列、または独立した2ブロックを同時に計算します。ARM64や、AVX2を持たないx64のプロセッサーなど、128ビットのベクトルを加速するほかのプロセッサーでは、128ビットのベクトル1つで複素数1つを計算します。変わるのは、独立したバタフライを評価する順序だけです。どの値も、演算の順序は同じで、積和命令は使いません。原典の擬似乱数は、直列の漸化式です。ここでは、同じ系列を8つの区間に分け、あらかじめ求めた開始状態から、AVX2では256ビットのベクトルで、それ以外では128ビットのベクトルで並列に生成し、原典と同じ順序で値を返します。Harvestの精密化は、候補ごとではなく、フレーム内の窓長ごとに、波形を一度だけ変換します。DioとHarvestが共有する零交差の検出は、1命令で複数のサンプルを比べます。CheapTrickは、リフターと窓を一度だけ求め、続くフレームのF0が同じ間は使い回します。合成と最小位相スペクトルで、補間と乗算と加算を行うループは、`Vector<double>`で動きます。これらの変更の前は、上の表のJITの列が、Dioで1.26x、StoneMaskで1.03x、CheapTrickで1.02x、D4Cで1.12x、Synthesisで0.93x、Harvestで1.01xでした。各処理を参照と照合する試験は、変更なしで通ります。128ビットのベクトルを加速しないプロセッサーでは、スカラーの処理を使い、同じ値を返します。

次の表は、Linux ARM64で一度だけ測りました。計測機は、GitHub Actionsの`ubuntu-24.04-arm`ランナーで、プロセッサーはNeoverse-N2です。比べた原典のC++は、GCCを使い`-O2`でビルドしています。単位はミリ秒で、15回実行した最小値です。毎回新しいプロセスで実行するので、JITの列には、コードのコンパイルと、階層型コンパイルが最適化する前のコードの実行が含まれます。この計測機では、NativeAOTのビルドは測っていません。128ビットの経路を足す前の同じ測定では、JITの列が、Dioで1.41x、StoneMaskで1.06x、CheapTrickで0.94x、D4Cで1.05x、Synthesisで0.95x、Harvestで1.86xでした。この表は固定の記録で、再生成しません。

| 処理 | GCCのC++ | 本移植のJIT | 比 |
|---|---:|---:|---:|
| Dio | 6.61 | 4.21 | 1.57x |
| StoneMask | 4.25 | 3.75 | 1.13x |
| CheapTrick | 8.42 | 7.14 | 1.18x |
| D4C | 31.41 | 26.43 | 1.19x |
| Synthesis | 9.36 | 8.95 | 1.05x |
| Harvest | 140.05 | 70.53 | 1.99x |

---

## APIリファレンス

### 解析

| メンバー | 説明 |
|---|---|
| `Dio.GetSamplesForDio(fs, xLength, framePeriod)` | 系列に含まれるフレームの個数を返します。 |
| `Dio.Estimate(x, fs, option, temporalPositions, f0, arena)` | F0の系列と時刻の系列を推定します。 |
| `Harvest.GetSamplesForHarvest(fs, xLength, framePeriod)` | 系列に含まれるフレームの個数を返します。 |
| `Harvest.Estimate(x, fs, option, temporalPositions, f0, arena)` | F0の系列と時刻の系列を推定します。 |
| `StoneMask.Refine(x, fs, temporalPositions, f0, refinedF0, arena)` | 既存の系列を瞬時周波数で精密化します。 |
| `CheapTrick.GetFftSize(fs, f0Floor)` | サンプリング周波数とF0の下限から定まるFFTサイズを返します。 |
| `CheapTrick.GetF0Floor(fs, fftSize)` | サンプリング周波数とFFTサイズから定まるF0の下限を返します。 |
| `CheapTrick.Estimate(x, fs, option, temporalPositions, f0, spectrogram, arena)` | スペクトル包絡を推定します。 |
| `D4C.Estimate(x, fs, option, temporalPositions, f0, fftSize, aperiodicity, arena)` | 帯域ごとの非周期性指標を推定します。 |

### 合成

| メンバー | 説明 |
|---|---|
| `Synthesis.GetSamplesForSynthesis(fs, f0Length, framePeriod)` | 波形に含まれるサンプルの個数を返します。 |
| `Synthesis.Synthesize(f0, spectrogram, aperiodicity, fftSize, framePeriod, fs, y, arena)` | 波形を一括で生成します。 |
| `new WorldSynthesizer(arena, fs, framePeriod, fftSize, bufferSize, numberOfPointers, maxFramesPerAdd)` | 実時間合成を生成します。 |
| `WorldSynthesizer.AddParameters(f0, spectrogram, aperiodicity)` | パラメーターの塊を投入し、受理したかどうかを返します。 |
| `WorldSynthesizer.Synthesize()` | 次の区間を生成し、出力が得られたかどうかを返します。 |
| `WorldSynthesizer.Buffer` | 現在の出力区間を参照します。 |
| `WorldSynthesizer.IsLocked` | 内部バッファーが満杯かどうかを返します。 |
| `WorldSynthesizer.Refresh()` | 投入済みのパラメーターと内部状態を破棄します。 |

### 符号化

| メンバー | 説明 |
|---|---|
| `Codec.GetNumberOfAperiodicities(fs)` | 非周期性指標の係数の個数を返します。 |
| `Codec.CodeAperiodicity(aperiodicity, f0Length, fs, fftSize, codedAperiodicity, arena)` | 非周期性指標を係数へ縮約します。 |
| `Codec.DecodeAperiodicity(codedAperiodicity, f0Length, fs, fftSize, aperiodicity, arena)` | 係数から非周期性指標を復元します。 |
| `Codec.CodeSpectralEnvelope(spectrogram, f0Length, fs, fftSize, numberOfDimensions, codedSpectralEnvelope, arena)` | スペクトル包絡をメル尺度上で縮約します。 |
| `Codec.DecodeSpectralEnvelope(codedSpectralEnvelope, f0Length, fs, fftSize, numberOfDimensions, spectrogram, arena)` | スペクトル包絡を復元します。 |

### メモリ

| メンバー | 説明 |
|---|---|
| `new WorldArena()` | 必要に応じて拡張するアリーナを生成します。 |
| `new WorldArena(initialCapacityInBytes)` | 初期の塊を伴うアリーナを生成します。 |
| `WorldArena.FromNativeMemory(buffer, capacityInBytes)` | 呼び出し側が所有する64バイト境界の領域を包みます。拡張しません。 |
| `WorldArena.GetReservedBytes(count, elementSize)` | 1回の確保が占める整列後の量を返します。 |
| `WorldArena.EnsureCapacity(byteCount)` | 指定した量が空くよう塊を追加します。 |
| `WorldArena.BeginScope()` | 位置を記録します。返された範囲を破棄すると復帰します。 |
| `WorldArena.Reset()` | 塊を保持したまま全ての確保を解放します。 |
| `WorldArena.Capacity`、`WorldArena.Used` | 全ての塊を通じた合計を返します。 |

### ファイル入出力

| メンバー | 説明 |
|---|---|
| `WaveFile.GetLength(path)` | ファイルに含まれるサンプルの個数を返します。 |
| `WaveFile.Read(path, destination, out sampleRate, out bitDepth)` | WAVファイルを読み込みます。 |
| `WaveFile.Write(path, x, sampleRate)` | 16ビットのWAVファイルを書き出します。 |
| `ParameterFile.WriteF0`、`ParameterFile.ReadF0` | F0の系列を解析パラメーターの形式で入出力します。 |
| `ParameterFile.WriteSpectralEnvelope`、`ParameterFile.ReadSpectralEnvelope` | スペクトル包絡を入出力します。 |
| `ParameterFile.WriteAperiodicity`、`ParameterFile.ReadAperiodicity` | 非周期性指標を入出力します。 |
| `ParameterFile.GetHeaderInformation(path, parameter)` | パラメーターファイルの見出しから1項目を読みます。 |

### オプションの既定値

| オプション | プロパティ | 既定値 | 説明 |
|---|---|---|---|
| `DioOption` | `F0Floor` | 71.0 | 探索範囲の下限です。単位はヘルツです。 |
| `DioOption` | `F0Ceil` | 800.0 | 探索範囲の上限です。単位はヘルツです。 |
| `DioOption` | `ChannelsInOctave` | 2.0 | 1オクターブあたりの帯域通過フィルターの本数です。 |
| `DioOption` | `FramePeriod` | 5.0 | フレーム周期です。単位はミリ秒です。 |
| `DioOption` | `Speed` | 1 | 1から12までの間引き比です。大きいほど速く粗くなります。 |
| `DioOption` | `AllowedRange` | 0.1 | 系列を補正する際のしきい値です。 |
| `HarvestOption` | `F0Floor` | 71.0 | 探索範囲の下限です。単位はヘルツです。 |
| `HarvestOption` | `F0Ceil` | 800.0 | 探索範囲の上限です。単位はヘルツです。 |
| `HarvestOption` | `FramePeriod` | 5.0 | フレーム周期です。単位はミリ秒です。 |
| `CheapTrickOption` | `Q1` | -0.15 | スペクトルの復元に用いる係数です。 |
| `CheapTrickOption` | `F0Floor` | 71.0 | FFTサイズを決めるF0の下限です。 |
| `CheapTrickOption` | `FftSize` | 導出値 | `CheapTrickOption.Create`がサンプリング周波数から算出します。 |
| `D4COption` | `Threshold` | 0.85 | D4C LoveTrainの処理のしきい値です。 |

`DioOption.Default`と`HarvestOption.Default`と`D4COption.Default`は、上の既定値を返します。`CheapTrickOption`はFFTサイズがサンプリング周波数に依存するため、代わりに`CheapTrickOption.Create(fs)`を使います。4つとも`init`アクセサーを持つ`readonly struct`なので、一部を変えた複製は`with`式で作れます。

---

## ビルド方法

.NET SDK 10.0を用意してリポジトリを取得し、直下で次のコマンドを実行します。

```sh
dotnet build WorldNet.slnx -c Release
```

パッケージの代わりにソースを使う場合は、`WorldNet/WorldNet.csproj`をプロジェクト参照として追加してください。

1. テストを実行する前に、Windowsでは`reference/build.bat`を、Linuxでは`reference/build.sh`を実行してください。どちらもWORLDを取得してビルドし、`reference/data`へ参照データを書き出します。
2. 実行例をNative AOTで発行する場合は`publish-aot.bat`を実行してください。

---

## 制限事項

- MSVCの参照に対して、一括合成は原典と完全には一致せず、差が最大振幅の4 ULP以内に収まります。Windows ARM64では、実時間合成も同じ範囲に収まります。D4Cと非周期性指標の復号の差は、1 ULP以内です。いずれも`pow`が原因です。この関数は、.NETのランタイムにもMSVCのランタイムにも、正しく丸めることが要求されていません。
- ビット単位の一致は、Windows x64でMSVCを使ってビルドしたWORLDと、Linux x64でGCC 13.3とglibc 2.39を使ってビルドしたWORLDに対して確認したものです。Linux ARM64では、GCCでビルドしたWORLDに対して、上の表の許容の幅の内側でテストが通ります。Windows ARM64でも、MSVCでビルドしたWORLDに対して、同じ範囲でテストが通ります。いずれの場合も、テストは.NET 8と.NET 10で通ります。他のコンパイラー、他のランタイム、他のアーキテクチャでは、超越関数の丸めが異なる可能性があるため、同じ一致は主張しません。
- macOSは、CIの対象にしていません。macOS 26のARM64でApple clang 21を使い、一度だけ実行しました。ライブラリはビルドでき、336件のうち318件のテストが通りました。失敗したのは、参照との照合と、.NET 10での`Math.SinCos`の恒等性のテストです。Apple clangは、同じ引数の`sin`と`cos`を、`__sincos_stret`の呼び出しへまとめます。この呼び出しは、別々に呼んだ場合と異なる値を返します。参照のビルドでこのまとめを止め、ライブラリが`Math.Sin`と`Math.Cos`を別々に呼ぶようにすると、失敗はD4Cの照合だけになりました。この照合も、コンパイラーの`pow`の扱いまで止めると通りました。そのため、macOSでは、原典とのビット単位の一致を主張しません。
- CIは、FFTと擬似乱数のベクトル演算を、AVX2を持つx64と、AVX2を持たないx64と、ARM64で実行します。ハードウェア命令をすべて無効にして、スカラーの処理も実行します。ランタイムが128ビットのベクトルの加速を報告するほかのプロセッサーでは、ARM64と同じ128ビットの経路を通りますが、CIはそこで実行していません。
- `WorldArena`はスレッドセーフではありません。並行して解析する場合は、スレッドごとにアリーナを分けてください。テストでこの形を検証しています。
- `FromNativeMemory`で生成したアリーナは拡張しません。必要量は、同じ呼び出しを拡張するアリーナで一度実行して`Capacity`を読み、その値に見出し用の64バイトを足した大きさ以上の領域として渡してください。`Used`は、各呼び出しが作業領域を解放するため、呼び出しの後は0になり、この用途には使えません。必要量は、入力の長さとサンプリング周波数とオプションによって変わります。
- 照合のテストは参照データを必要とします。`reference/data`が存在しない場合、該当のテストは飛ばされ、残りのテストは参照データに依存しません。
- `WorldNet.Examples`配下の実行例はファイルを読み書きするため、マネージドヒープを確保します。無確保の保証はライブラリに対するものです。

---

## 注意事項

- 処理負荷: Harvestが最も重く、次にD4Cが重くなります。F0の系列だけが目的なら、`Speed`を上げたDioが最も軽くなります。性能の表の数値は一つの環境で得たもので、実行のたびにも変わります。`WorldNet.Examples bench <input.wav>`を実行すると、手元の環境での処理ごとの所要時間を表示します。参照ハーネスも、`WORLD_BENCH_ONLY`を設定すると、原典のC++について同じ形式で表示します。
- アリーナの使い回し: アリーナは初回の呼び出しで拡張し、以降は確保しません。呼び出しのたびに生成し直すと、この利点が失われ、ネイティブの確保が再び発生します。
- 決定性: 処理は、繰り返し実行しても同じ出力を返します。D4Cと合成が使う擬似乱数は、原典と同じxorshiftです。同じ状態から開始し、原典の系列を正確に再現します。AVX2がある場合と、ほかのプロセッサーで128ビットのベクトルを加速する場合は、系列を8つの区間に分けて並列に計算しますが、値は原典と同じ順序で返します。ベクトルを加速しない場合は、原典の漸化式をそのまま実行します。
- 作業領域の記述: `[ScratchLayout]`を付けた型は、`IScratchAllocator`を型引数に取る`Layout`メソッドを備える必要があります。ジェネレーターは、引数の並びを合わせた`GetRequiredArenaBytes`と`Bind`を生成します。型がすでに宣言しているほうは、生成しません。
- Native AOT: ライブラリは`IsAotCompatible`を指定しており、トリム、単一ファイル、AOTの各アナライザーが有効になります。`publish-aot.bat`は、実行例を`win-x64`向けに発行します。ネイティブのリンクには、MSVCのツールセットが必要です。
- JITコンパイル: ループを含む処理とOoura FFTには`MethodImplOptions.AggressiveOptimization`を付けています。これらの処理は、最適化されていない最初の階層を通らず、最初の呼び出しで完全に最適化してコンパイルされます。そのため、プロセスの最初の呼び出しにはコンパイルの時間がかかります。プロセスが暖まった後の速さは、指定の有無で変わりません。
- 正弦と余弦: `net10.0`向けのビルドは、最小位相のスペクトルを`Math.SinCos`で求めます。`net8.0`向けのビルドは、`Math.Sin`と`Math.Cos`を別々に呼びます。Windowsの.NET 8では、`Math.SinCos`の結果が、別々に呼んだ結果と1 ULP異なり、原典との一致が崩れるためです。
- 参照データの再生成: Windowsでは`reference/build.bat`が、Linuxでは`reference/build.sh`が、WORLDを`reference/world-src`へ取得してビルドし、出力を書き出します。どちらのディレクトリもバージョン管理の対象外です。
- ネットワーク通信: 本ライブラリはネットワークへ接続せず、データを送信しません。

---

## 不具合の報告

不具合は、GitHubの[Issues](https://github.com/routersys/WorldNet/issues)へ報告してください。本ライブラリは、不具合の内容を自動で送信しません。

報告には次の内容を書いてください。

| 項目 | 内容 |
|---|---|
| 環境 | WorldNetのバージョン、.NET SDKまたはランタイムのバージョン、OS、プロセッサーのアーキテクチャ |
| 入力 | 波形のサンプリング周波数と長さ、問題が現れる処理 |
| 手順 | 呼び出した順序と、既定値から変えたオプション |
| 結果 | 期待した結果と、実際の結果。数値の差であれば、処理、添字、両方の値 |
| 例外 | 型、メッセージ、スタックトレースを、そのまま |

---

## 免責事項

本ライブラリはMITライセンスのもとで公開されています。

本ソフトウェアは「現状のまま」提供されており、明示・黙示を問わず、商品性、特定目的への適合性、および権利非侵害に関する保証を含む、いかなる種類の保証も行いません。

作者は、本ライブラリの使用または使用不能に起因するいかなる損害についても、一切の責任を負いません。
ご利用は自己責任でお願いします。

---

## サードパーティライセンス

WorldNetは以下のソフトウェアの派生物です。ライセンスの全文は、リポジトリの[`.github/LICENSE/WORLD.txt`](.github/LICENSE/WORLD.txt)と[`.github/LICENSE/OouraFFT.txt`](.github/LICENSE/OouraFFT.txt)と、NuGetパッケージの`THIRD-PARTY-NOTICES/`に収録しています。

WORLDは修正BSDライセンスで配布されています。このライセンスは、ソースコードを再配布するとき、著作権表示、条件の一覧、免責の文面を保持するよう求めています。上のファイルは、その文面を改変せずに収録しています。本リポジトリはサードパーティのソースコードを同梱していません。参照ハーネスが、必要に応じてWORLDを取得します。

| ソフトウェア | 用途 | ライセンス | 著作権表示 |
|---|---|---|---|
| [WORLD](https://github.com/mmorise/World) | 移植したすべてのアルゴリズムの出典と、検証に使う参照実装 | 修正BSDライセンス | Copyright (c) 2010 M. Morise |
| [Ooura FFT](https://www.kurims.kyoto-u.ac.jp/~ooura/fft.html) | WORLDが収録し本ライブラリが移植した高速フーリエ変換 | 作者が自由な利用を許諾 | Copyright Takuya OOURA, 1996-2001 |

---

## ライセンス

[MIT License](LICENSE.txt)
