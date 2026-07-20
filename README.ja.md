# WorldNet

[![License](https://img.shields.io/badge/license-MIT-blue.svg)](#)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)](#)
[![Release](https://img.shields.io/github/v/release/routersys/WorldNet.svg)](https://github.com/routersys/WorldNet/releases)

[English](README.md) | 日本語

---

M. Morise氏による音声分析変換合成システム[WORLD](https://github.com/mmorise/World)をC#へ完全移植したライブラリです。
全ての段がマネージドヒープを一切確保せずに動作するため、ガベージコレクタが解析と合成の経路を観測しません。
作業領域はネイティブのアリーナから供給し、主要な演算はunsafeなポインタで記述し、ライブラリ全体をNative AOT向けに注釈しています。
正しさはソースの読解ではなく、MSVCでビルドした原典C++が出力した基準データとの突合で確認しています。

---

## 目次

1. [概要](#概要)
2. [動作要件](#動作要件)
3. [導入方法](#導入方法)
4. [主な機能](#主な機能)
   - [1. 基本周波数の推定](#1-基本周波数の推定)
   - [2. スペクトル包絡と非周期性指標](#2-スペクトル包絡と非周期性指標)
   - [3. 波形合成](#3-波形合成)
   - [4. スペクトル包絡の符号化](#4-スペクトル包絡の符号化)
   - [5. 無確保とアリーナ](#5-無確保とアリーナ)
   - [6. 数値検証](#6-数値検証)
   - [7. 性能](#7-性能)
5. [APIリファレンス](#apiリファレンス)
   - [解析](#解析)
   - [合成](#合成)
   - [符号化](#符号化)
   - [メモリ](#メモリ)
   - [ファイル入出力](#ファイル入出力)
   - [オプションの既定値](#オプションの既定値)
6. [制限事項](#制限事項)
7. [注意事項](#注意事項)
8. [免責事項](#免責事項)
9. [サードパーティライセンス](#サードパーティライセンス)
10. [ライセンス](#ライセンス)

---

## 概要

WorldNetはWORLDの11個のソースファイルをC#へ移植しています。基本周波数の推定はDioとHarvestとStoneMaskが担い、スペクトル包絡の推定はCheapTrickが、非周期性指標の推定はD4Cが、波形の生成は一括合成と逐次合成が、低次元表現はスペクトル包絡の符号化が担います。WAVの入出力と解析パラメータのファイル形式も移植の対象です。

公開する面はモダンなC#です。波形とパラメータは`ReadOnlySpan<double>`と`Span<double>`で受け渡し、オプションは`init`アクセサを持つ`readonly struct`として既定値の生成器を備え、各算法は静的クラスとして公開します。ポインタを用いる内部実装は公開しません。

作業領域は全て`WorldArena`が供給します。`NativeMemory.AlignedAlloc`による64バイト境界の確保をバンプ方式で切り出す構造で、塊を連結して管理するため、容量が増えても既に渡したポインタは無効になりません。作業領域の要求は専用の型の`Layout`メソッドに一度だけ記述します。ソースジェネレータがその署名を読んで必要量の照会と束縛の両方を生成するため、報告する必要量と実際の消費量が食い違いません。

原典のC++は本リポジトリに同梱していません。`reference`配下の参照ハーネスがWORLDを取得してMSVCでビルドし、各段の入力と出力を倍精度のまま書き出します。テストはその出力を読み込んでC#側の結果と突き合わせます。

---

## 動作要件

| 項目 | 要件 |
|---|---|
| OS | .NET 10が対応するWindows、Linux、macOS |
| SDK | .NET SDK 10.0 |
| 言語 | C# 14以降。`LangVersion`は`latest`を指定しています |
| unsafeコード | `WorldArena.FromNativeMemory`を使う場合のみ、利用側の設定が必要です |
| 基準データ | MSVCのC++ツールセットとGit。テストが使う基準データを再生成する場合のみ必要です |

---

## 導入方法

1. リポジトリを取得し、`WorldNet/WorldNet.csproj`をプロジェクト参照として追加するか、ビルドした成果物を参照してください。
2. `WorldArena`は一度だけ生成し、以後の呼び出しで使い回してください。アリーナは初回の呼び出しで拡張し、以降は追加の確保を行いません。
3. テストを実行する前に`reference/build.bat`を実行してください。WORLDを取得してMSVCでビルドし、`reference/data`へ基準データを書き出します。
4. 実行例をNative AOTで発行する場合は`publish-aot.bat`を実行してください。

---

## 主な機能

### 1. 基本周波数の推定

Dioは帯域通過させた信号の周期性からF0の系列を推定し、時刻の系列と併せて返します。`Speed`オプションを上げると間引きの経路を通り、探索の前に標本化周波数を落とします。Harvestは瞬時周波数による候補から系列を推定し、計算量と引き換えにより頑健な結果を返します。StoneMaskは既存の系列を瞬時周波数で精密化するもので、通常はDioの出力に適用します。

DioとHarvestとStoneMaskはいずれも、基準波形において原典とビット単位で一致します。Dioの間引きの経路も一致します。

### 2. スペクトル包絡と非周期性指標

CheapTrickはF0に適応した窓とピッチ同期の平滑化によってスペクトル包絡を推定します。FFTの寸法は標本化周波数とF0の下限から`CheapTrick.GetFftSize`が導き、`CheapTrick.GetF0Floor`が逆の関係を返します。

D4Cは帯域ごとの非周期性指標を推定し、D4C LoveTrainの段を含みます。結果は原典と1 ULP以内で一致します。差の原因は`Math.Pow`にあり、この関数は正しい丸めを要求されておらず、MSVCの実行時ライブラリと常に同じ値を返すとは限りません。

### 3. 波形合成

`Synthesis.Synthesize`はF0の系列とスペクトログラムと非周期性指標から波形を一括で生成します。`WorldSynthesizer`は逐次合成を実装しており、`AddParameters`でパラメータの塊を受け取り、内部の環状バッファを管理しながら`Synthesize`で出力を生成します。

逐次合成は原典とビット単位で一致します。一括合成は64 ULP以内で一致します。これは同じ`Math.Pow`の差が重畳加算の累積によって拡大したものです。

### 4. スペクトル包絡の符号化

符号化はスペクトル包絡と非周期性指標をメル尺度上の低次元表現へ変換し、また元へ戻します。`Codec.GetNumberOfAperiodicities`は標本化周波数に対する係数の個数を返します。

非周期性指標の符号化と、スペクトル包絡の符号化および復号は、原典とビット単位で一致します。非周期性指標の復号のみ1 ULP以内の一致で、これも`Math.Pow`に起因します。

### 5. 無確保とアリーナ

`WorldArena`はネイティブの塊の連結から64バイト境界の領域を切り出します。`BeginScope`が現在位置を記録し、破棄時に復帰するため、繰り返しの内側で取った作業領域を解放処理なしで戻せます。`FromNativeMemory`は呼び出し側が所有する領域を包みます。この形態のアリーナは拡張せず、容量が尽きると例外を投げます。

作業領域の要求は、確保器を型引数に取る`Layout`メソッドとして一度だけ記述します。測定用の確保器で実行すればメモリに触れずに必要量が求まり、アリーナの確保器で実行すれば実際の束縛が行われます。ソースジェネレータはこの一つの署名から`GetRequiredArenaBytes`と`Bind`を生成します。

マネージドヒープを確保しないことは主張ではなく測定で確かめています。`GC.GetAllocatedBytesForCurrentThread`は、解析から合成までの全経路と逐次合成のいずれについても増分0バイトを報告します。

### 6. 数値検証

テストはMSVCでビルドした原典C++の出力と突き合わせます。テストが強制する一致の水準は次の通りです。

| 対象 | 一致の水準 |
|---|---|
| 大浦FFT、4種の変換、寸法8から4096 | 完全一致 |
| matlabfunctions、common | 完全一致 |
| Dio、間引きの経路を含む | 完全一致 |
| StoneMask、Harvest、CheapTrick | 完全一致 |
| 逐次合成、WAV入出力、パラメータファイル入出力 | 完全一致 |
| 非周期性指標の符号化、スペクトル包絡の符号化と復号 | 完全一致 |
| D4C、非周期性指標の復号 | 1 ULP以内 |
| 一括合成 | 64 ULP以内 |

超越関数は個別に測定しています。`Math.Cos`と`Math.Sin`と`Math.Log`と`Math.Exp`と`Math.Log10`は、標本化した範囲においてMSVCの実行時ライブラリと同一の倍精度値を返します。`Math.Pow(10, v)`と二乗の`v * v`はMSVCの`pow`と最大1 ULP異なり、その頻度は標本の1%未満です。上の許容はここから導かれます。

等価性の確認に加えて、無音と直流と白色雑音による退化した入力、極端に短い入力、繰り返し実行での決定性、スレッドごとにアリーナを分けた場合の安全性、呼び出し側が供給したアリーナでの動作、経路終了後のアリーナの完全な解放を網羅しています。テストは330件あり、全て通過します。

### 7. 性能

MSVCで`/O2`を指定してビルドした原典C++と、本移植をNative AOTで発行したものとを比較します。どちらも事前コンパイルですので、条件を揃えた比較になります。数値は10回実行した最小値で、単位はミリ秒です。測定環境はIntel Core i7-1360Pを搭載したWindows 11で、標本化周波数22050Hz、17500標本の基準波形を枠の移動量5ミリ秒で解析しています。

| 段 | MSVCのC++ | 本移植のNative AOT |
|---|---:|---:|
| Dio | 18.23 | 10.00 |
| StoneMask | 9.21 | 7.42 |
| CheapTrick | 20.01 | 21.27 |
| D4C | 67.04 | 91.94 |
| Synthesis | 17.89 | 19.22 |
| Harvest | 356.30 | 306.08 |

実行時コンパイラを通した数値は意図して載せていません。計測が各段を2回しか繰り返さないため、段階的コンパイルが安定しないからです。`DOTNET_TieredCompilation=0`で段階化を無効にすると、実行時コンパイラの数値はNative AOTの列とほぼ一致します。したがってこの差は計測手法による暖機不足であり、移植の性質ではありません。

---

## APIリファレンス

### 解析

| メンバー | 説明 |
|---|---|
| `Dio.GetSamplesForDio(fs, xLength, framePeriod)` | 系列に含まれる枠の個数を返します。 |
| `Dio.Estimate(x, fs, option, temporalPositions, f0, arena)` | F0の系列と時刻の系列を推定します。 |
| `Harvest.GetSamplesForHarvest(fs, xLength, framePeriod)` | 系列に含まれる枠の個数を返します。 |
| `Harvest.Estimate(x, fs, option, temporalPositions, f0, arena)` | F0の系列と時刻の系列を推定します。 |
| `StoneMask.Refine(x, fs, temporalPositions, f0, refinedF0, arena)` | 既存の系列を瞬時周波数で精密化します。 |
| `CheapTrick.GetFftSize(fs, f0Floor)` | 標本化周波数とF0の下限から定まるFFTの寸法を返します。 |
| `CheapTrick.GetF0Floor(fs, fftSize)` | 標本化周波数とFFTの寸法から定まるF0の下限を返します。 |
| `CheapTrick.Estimate(x, fs, option, temporalPositions, f0, spectrogram, arena)` | スペクトル包絡を推定します。 |
| `D4C.Estimate(x, fs, option, temporalPositions, f0, fftSize, aperiodicity, arena)` | 帯域ごとの非周期性指標を推定します。 |

### 合成

| メンバー | 説明 |
|---|---|
| `Synthesis.Synthesize(f0, spectrogram, aperiodicity, fftSize, framePeriod, fs, y, arena)` | 波形を一括で生成します。 |
| `new WorldSynthesizer(arena, fs, framePeriod, fftSize, bufferSize, numberOfPointers, maxFramesPerAdd)` | 逐次合成を生成します。 |
| `WorldSynthesizer.AddParameters(f0, spectrogram, aperiodicity)` | パラメータの塊を投入し、受理したかどうかを返します。 |
| `WorldSynthesizer.Synthesize()` | 次の区間を生成し、出力が得られたかどうかを返します。 |
| `WorldSynthesizer.Buffer` | 現在の出力区間を参照します。 |
| `WorldSynthesizer.IsLocked` | 内部バッファが満杯かどうかを返します。 |
| `WorldSynthesizer.Refresh()` | 投入済みのパラメータと内部状態を破棄します。 |

### 符号化

| メンバー | 説明 |
|---|---|
| `Codec.GetNumberOfAperiodicities(fs)` | 非周期性指標の係数の個数を返します。 |
| `Codec.CodeAperiodicity(aperiodicity, f0Length, fs, fftSize, codedAperiodicity)` | 非周期性指標を係数へ縮約します。 |
| `Codec.DecodeAperiodicity(codedAperiodicity, f0Length, fs, fftSize, aperiodicity)` | 係数から非周期性指標を復元します。 |
| `Codec.CodeSpectralEnvelope(spectrogram, f0Length, fs, fftSize, dimensions, coded)` | スペクトル包絡をメル尺度上で縮約します。 |
| `Codec.DecodeSpectralEnvelope(coded, f0Length, fs, fftSize, dimensions, spectrogram)` | スペクトル包絡を復元します。 |

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
| `WaveFile.GetLength(path)` | ファイルに含まれる標本の個数を返します。 |
| `WaveFile.Read(path, destination, out sampleRate, out bitDepth)` | WAVファイルを読み込みます。 |
| `WaveFile.Write(path, x, sampleRate)` | 16ビットのWAVファイルを書き出します。 |
| `ParameterFile.WriteF0`、`ParameterFile.ReadF0` | F0の系列を解析パラメータの形式で入出力します。 |
| `ParameterFile.WriteSpectralEnvelope`、`ParameterFile.ReadSpectralEnvelope` | スペクトル包絡を入出力します。 |
| `ParameterFile.WriteAperiodicity`、`ParameterFile.ReadAperiodicity` | 非周期性指標を入出力します。 |
| `ParameterFile.GetHeaderInformation(path, parameter)` | パラメータファイルの見出しから1項目を読みます。 |

### オプションの既定値

| オプション | プロパティ | 既定値 | 説明 |
|---|---|---|---|
| `DioOption` | `F0Floor` | 71.0 | 探索範囲の下限です。単位はヘルツです。 |
| `DioOption` | `F0Ceil` | 800.0 | 探索範囲の上限です。単位はヘルツです。 |
| `DioOption` | `ChannelsInOctave` | 2.0 | 1オクターブあたりの帯域通過フィルタの本数です。 |
| `DioOption` | `FramePeriod` | 5.0 | 枠の移動量です。単位はミリ秒です。 |
| `DioOption` | `Speed` | 1 | 1から12までの間引き比です。大きいほど速く粗くなります。 |
| `DioOption` | `AllowedRange` | 0.1 | 系列を補正する際のしきい値です。 |
| `HarvestOption` | `F0Floor` | 71.0 | 探索範囲の下限です。単位はヘルツです。 |
| `HarvestOption` | `F0Ceil` | 800.0 | 探索範囲の上限です。単位はヘルツです。 |
| `HarvestOption` | `FramePeriod` | 5.0 | 枠の移動量です。単位はミリ秒です。 |
| `CheapTrickOption` | `Q1` | -0.15 | スペクトルの復元に用いる係数です。 |
| `CheapTrickOption` | `F0Floor` | 71.0 | FFTの寸法を決めるF0の下限です。 |
| `CheapTrickOption` | `FftSize` | 導出値 | `CheapTrickOption.Create`が標本化周波数から算出します。 |
| `D4COption` | `Threshold` | 0.85 | D4C LoveTrainの段のしきい値です。 |

`DioOption.Default`と`HarvestOption.Default`と`D4COption.Default`は上の値を返します。`CheapTrickOption`はFFTの寸法が標本化周波数に依存するため、代わりに`CheapTrickOption.Create(fs)`を使います。4つとも`init`アクセサを持つ`readonly struct`ですので、一部を変えた複製は`with`式で作れます。

---

## 制限事項

- 一括合成は原典と完全には一致せず、64 ULP以内の一致です。D4Cと非周期性指標の復号は1 ULP以内の一致です。いずれも`Math.Pow`に起因します。この関数は.NETの実行時ライブラリにもMSVCの実行時ライブラリにも正しい丸めが要求されていません。
- ビット単位の一致は、Windows x64においてMSVCでビルドしたWORLDに対して確認したものです。他のコンパイラ、他の実行時ライブラリ、他の命令セットでは超越関数の丸めが異なる可能性があり、同じ一致を主張しません。
- `WorldArena`はスレッド安全ではありません。並行して解析する場合はスレッドごとにアリーナを分けてください。テストはこの形態を検証しています。
- `FromNativeMemory`で生成したアリーナは拡張しません。必要量は、拡張するアリーナで一度実行して`Used`を読むか、利用する段の`GetRequiredArenaBytes`を合計して求めてください。
- 突合のテストは基準データを必要とします。`reference/data`が存在しない場合、該当のテストは実行できません。
- `WorldNet.Examples`配下の実行例はファイルを読み書きするため、マネージドヒープを確保します。無確保の保証はライブラリに対するものです。

---

## 注意事項

- 処理負荷: Harvestが最も重く、次いでD4Cが重くなります。系列を得るだけであれば`Speed`を上げたDioが最も軽くなります。性能の表の数値は一つの環境で得たものであり、実行ごとにも変動します。`WorldNet.Examples bench <input.wav>`が手元の環境での段別の所要時間を表示します。参照ハーネスも`WORLD_BENCH_ONLY`を設定すると、原典C++について同じ形式で表示します。
- アリーナの使い回し: アリーナは初回の呼び出しで拡張し、以降は確保を行いません。呼び出しごとに生成し直すと利点が失われ、ネイティブの確保が再び発生します。
- 決定性: 経路は繰り返し実行しても同一の出力を返します。D4Cと合成が使う擬似乱数は原典と同じxorshiftで、同じ状態から開始し、系列と最終状態まで原典を再現します。
- 作業領域の記述: `[ScratchLayout]`を付けた型は、`IScratchAllocator`を型引数に取る`Layout`メソッドを備える必要があります。ジェネレータは引数の並びを揃えた`GetRequiredArenaBytes`と`Bind`を生成し、型が既に宣言している側は生成しません。
- Native AOT: ライブラリは`IsAotCompatible`を指定しており、トリムと単一ファイルとAOTの各解析器が有効になります。`publish-aot.bat`は実行例を`win-x64`向けに発行します。ネイティブのリンクにMSVCのツールセットが必要です。
- 基準データの再生成: `reference/build.bat`がWORLDを`reference/world-src`へ取得してビルドし、出力を書き出します。どちらのディレクトリもバージョン管理の対象外です。

---

## 免責事項

本ライブラリはMITライセンスのもとで公開されています。

本ソフトウェアは「現状のまま」提供されており、明示または黙示を問わず、商品性、特定目的への適合性、および権利非侵害に関する保証を含む、いかなる種類の保証も行いません。

作者は、本ライブラリの使用または使用不能に起因するいかなる損害についても、一切の責任を負いません。ご利用は自己責任でお願いします。

---

## サードパーティライセンス

WorldNetは以下のソフトウェアの派生物です。ライセンスの全文は、リポジトリの[`.github/LICENSE/WORLD.txt`](.github/LICENSE/WORLD.txt)と[`.github/LICENSE/OouraFFT.txt`](.github/LICENSE/OouraFFT.txt)に収録しています。

WORLDは修正BSDライセンスで配布されており、ソース形式での再配布に際して著作権表示と条件文と免責文を保持することを求めます。上記のファイルはその文面を改変せずに収録しています。本リポジトリはサードパーティのソースコードを同梱しておらず、参照ハーネスが必要に応じてWORLDを取得します。

| ソフトウェア | 用途 | ライセンス | 著作権表示 |
|---|---|---|---|
| [WORLD](https://github.com/mmorise/World) | 移植した全ての算法の出典、および検証に用いる参照実装 | 修正BSDライセンス | Copyright (c) 2010 M. Morise |
| [大浦FFT](https://www.kurims.kyoto-u.ac.jp/~ooura/fft.html) | WORLDが収録し本ライブラリが移植した高速フーリエ変換 | 作者が自由な利用を許諾 | Copyright Takuya OOURA, 1996-2001 |

---

## ライセンス

[MIT License](LICENSE.txt)
