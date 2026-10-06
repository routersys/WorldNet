import argparse
import os
import platform
import subprocess
import sys
from datetime import datetime, timezone

STAGES = ["Dio", "StoneMask", "CheapTrick", "D4C", "Synthesis", "Harvest"]
BEGIN = "<!-- BENCHMARK:CI:BEGIN -->"
END = "<!-- BENCHMARK:CI:END -->"


def run_once(command, environment):
    completed = subprocess.run(command, capture_output=True, text=True, env=environment)
    if completed.returncode != 0:
        sys.stderr.write(completed.stdout)
        sys.stderr.write(completed.stderr)
        raise SystemExit(f"benchmark command failed: {' '.join(command)}")
    values = {}
    for line in completed.stdout.splitlines():
        parts = line.split()
        if len(parts) == 3 and parts[0] == "BENCH":
            values[parts[1]] = float(parts[2])
    return values


def measure(command, environment, runs):
    best = {}
    for _ in range(runs):
        for stage, value in run_once(command, environment).items():
            if stage not in best or value < best[stage]:
                best[stage] = value
    missing = [stage for stage in STAGES if stage not in best]
    if missing:
        raise SystemExit(f"missing stages from {' '.join(command)}: {', '.join(missing)}")
    return best


def processor_name():
    try:
        completed = subprocess.run(
            ["powershell", "-NoProfile", "-Command",
             "(Get-CimInstance Win32_Processor).Name"],
            capture_output=True, text=True)
        lines = [line.strip() for line in completed.stdout.splitlines() if line.strip()]
        if lines:
            return lines[0]
    except OSError:
        pass
    return platform.processor() or "an unidentified processor"


def rows(cpp, aot, jit):
    for stage in STAGES:
        yield (stage, cpp[stage], aot[stage], cpp[stage] / aot[stage],
               jit[stage], cpp[stage] / jit[stage])


def english_block(cpp, aot, jit, runs, cpu, stamp, commit):
    lines = [
        BEGIN,
        "",
        f"Measured by CI on a GitHub Actions `windows-latest` runner with {cpu}. "
        f"Figures are the best of {runs} runs in milliseconds, analysing the 22050 Hz "
        f"reference waveform of 17500 samples with a 5 ms frame period. "
        f"Every run of the port is a fresh process, so the just-in-time column "
        f"includes the cost of compiling the code and of running it before tiered "
        f"compilation has optimized it. "
        f"All builds run back to back in the same job, so the ratio is the stable "
        f"quantity; the absolute values move with the shared runner. "
        f"Recorded on {stamp} from commit `{commit}`.",
        "",
        "| Stage | C++ with MSVC | This port with Native AOT | Ratio "
        "| This port with JIT | Ratio |",
        "|---|---:|---:|---:|---:|---:|",
    ]
    for stage, a, b, ratio, c, jit_ratio in rows(cpp, aot, jit):
        lines.append(
            f"| {stage} | {a:.2f} | {b:.2f} | {ratio:.2f}x | {c:.2f} | {jit_ratio:.2f}x |")
    lines.append("")
    lines.append(END)
    return "\n".join(lines)


def japanese_block(cpp, aot, jit, runs, cpu, stamp, commit):
    lines = [
        BEGIN,
        "",
        f"GitHub Actionsの`windows-latest`ランナー上で、CIが計測した値です。"
        f"プロセッサーは{cpu}です。{runs}回実行した最小値を、ミリ秒で示します。"
        f"サンプリング周波数22050Hz、17500サンプルの参照波形を、フレーム周期5ミリ秒で解析しています。"
        f"本移植は毎回新しいプロセスで実行するので、"
        f"JITの列には、コードのコンパイルと、階層型コンパイルが最適化する前のコードの実行が含まれます。"
        f"すべてを同じジョブの中で続けて実行するので、安定する量は比で、"
        f"絶対値は共有ランナーの状態によって変わります。"
        f"計測日は{stamp}、対象のコミットは`{commit}`です。",
        "",
        "| 処理 | MSVCのC++ | 本移植のNativeAOT | 比 | 本移植のJIT | 比 |",
        "|---|---:|---:|---:|---:|---:|",
    ]
    for stage, a, b, ratio, c, jit_ratio in rows(cpp, aot, jit):
        lines.append(
            f"| {stage} | {a:.2f} | {b:.2f} | {ratio:.2f}x | {c:.2f} | {jit_ratio:.2f}x |")
    lines.append("")
    lines.append(END)
    return "\n".join(lines)


def replace_block(path, block):
    with open(path, encoding="utf-8") as handle:
        text = handle.read()
    start = text.find(BEGIN)
    finish = text.find(END)
    if start < 0 or finish < 0 or finish < start:
        raise SystemExit(f"benchmark markers not found in {path}")
    updated = text[:start] + block + text[finish + len(END):]
    with open(path, "w", encoding="utf-8", newline="\n") as handle:
        handle.write(updated)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--cpp", required=True)
    parser.add_argument("--aot", required=True)
    parser.add_argument("--jit", required=True)
    parser.add_argument("--wav", required=True)
    parser.add_argument("--data", required=True)
    parser.add_argument("--runs", type=int, default=20)
    parser.add_argument("--readme", required=True)
    parser.add_argument("--readme-ja", required=True)
    parser.add_argument("--commit", default="unknown")
    arguments = parser.parse_args()

    cpp_path = os.path.abspath(arguments.cpp)
    aot_path = os.path.abspath(arguments.aot)
    jit_path = os.path.abspath(arguments.jit)
    wav_path = os.path.abspath(arguments.wav)
    data_path = os.path.abspath(arguments.data)

    cpp_environment = dict(os.environ)
    cpp_environment["WORLD_BENCH_ONLY"] = "1"
    cpp = measure([cpp_path, wav_path, data_path], cpp_environment, arguments.runs)

    port_environment = dict(os.environ)
    port_environment.pop("WORLD_BENCH_ONLY", None)
    aot = measure([aot_path, "bench", wav_path], port_environment, arguments.runs)
    jit = measure(["dotnet", jit_path, "bench", wav_path], port_environment, arguments.runs)

    cpu = processor_name()
    stamp = datetime.now(timezone.utc).strftime("%Y-%m-%d")
    commit = arguments.commit[:7]

    replace_block(arguments.readme,
                  english_block(cpp, aot, jit, arguments.runs, cpu, stamp, commit))
    replace_block(arguments.readme_ja,
                  japanese_block(cpp, aot, jit, arguments.runs, cpu, stamp, commit))

    for stage, a, b, ratio, c, jit_ratio in rows(cpp, aot, jit):
        print(f"{stage:<12} cpp={a:8.2f} aot={b:8.2f} ratio={ratio:.2f} "
              f"jit={c:8.2f} ratio={jit_ratio:.2f}")


if __name__ == "__main__":
    main()
