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


def rows(cpp, aot):
    for stage in STAGES:
        yield stage, cpp[stage], aot[stage], cpp[stage] / aot[stage]


def english_block(cpp, aot, runs, cpu, stamp, commit):
    lines = [
        BEGIN,
        "",
        f"Measured by CI on a GitHub Actions `windows-latest` runner with {cpu}. "
        f"Figures are the best of {runs} runs in milliseconds, analysing the 22050 Hz "
        f"reference waveform of 17500 samples with a 5 ms frame period. "
        f"Both builds run back to back in the same job, so the ratio is the stable "
        f"quantity; the absolute values move with the shared runner. "
        f"Recorded on {stamp} from commit `{commit}`.",
        "",
        "| Stage | C++ with MSVC | This port with Native AOT | Ratio |",
        "|---|---:|---:|---:|",
    ]
    for stage, a, b, ratio in rows(cpp, aot):
        lines.append(f"| {stage} | {a:.2f} | {b:.2f} | {ratio:.2f}x |")
    lines.append("")
    lines.append(END)
    return "\n".join(lines)


def japanese_block(cpp, aot, runs, cpu, stamp, commit):
    lines = [
        BEGIN,
        "",
        f"GitHub Actionsの`windows-latest`ランナー上でCIが計測した値です。"
        f"搭載する演算装置は{cpu}です。{runs}回実行した最小値を掲載しており、単位はミリ秒です。"
        f"標本化周波数22050Hz、17500標本の基準波形を枠の移動量5ミリ秒で解析しています。"
        f"両者は同一のジョブ内で連続して実行しますので、安定する量は比であり、"
        f"絶対値は共有ランナーの状態に応じて変動します。"
        f"計測日は{stamp}、対象のコミットは`{commit}`です。",
        "",
        "| 段 | MSVCのC++ | 本移植のNativeAOT | 比 |",
        "|---|---:|---:|---:|",
    ]
    for stage, a, b, ratio in rows(cpp, aot):
        lines.append(f"| {stage} | {a:.2f} | {b:.2f} | {ratio:.2f}x |")
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
    parser.add_argument("--wav", required=True)
    parser.add_argument("--data", required=True)
    parser.add_argument("--runs", type=int, default=20)
    parser.add_argument("--readme", required=True)
    parser.add_argument("--readme-ja", required=True)
    parser.add_argument("--commit", default="unknown")
    arguments = parser.parse_args()

    cpp_path = os.path.abspath(arguments.cpp)
    aot_path = os.path.abspath(arguments.aot)
    wav_path = os.path.abspath(arguments.wav)
    data_path = os.path.abspath(arguments.data)

    cpp_environment = dict(os.environ)
    cpp_environment["WORLD_BENCH_ONLY"] = "1"
    cpp = measure([cpp_path, wav_path, data_path], cpp_environment, arguments.runs)

    aot_environment = dict(os.environ)
    aot_environment.pop("WORLD_BENCH_ONLY", None)
    aot = measure([aot_path, "bench", wav_path], aot_environment, arguments.runs)

    cpu = processor_name()
    stamp = datetime.now(timezone.utc).strftime("%Y-%m-%d")
    commit = arguments.commit[:7]

    replace_block(arguments.readme,
                  english_block(cpp, aot, arguments.runs, cpu, stamp, commit))
    replace_block(arguments.readme_ja,
                  japanese_block(cpp, aot, arguments.runs, cpu, stamp, commit))

    for stage, a, b, ratio in rows(cpp, aot):
        print(f"{stage:<12} cpp={a:8.2f} aot={b:8.2f} ratio={ratio:.2f}")


if __name__ == "__main__":
    main()
