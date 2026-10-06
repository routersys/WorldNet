#!/usr/bin/env bash
set -e
K="$RUNNER_TEMP/kit"
rm -rf "$K"
mkdir -p "$K"
cp -r .profkit/* "$K/"
W="$GITHUB_WORKSPACE/reference/world-src"
WAV="$W/test/vaiueo2d.wav"

others() {
  ls "$W"/src/*.cpp | grep -v "synthesisrealtime" | grep -v "$1"
}

g++ -O2 -ffp-contract=off -w -I "$W/src" -I "$W/tools" -I "$K/cpp-syn" -o "$K/prof_syn" \
  "$K/cpp-syn/prof_main.cpp" "$K/cpp-syn/syn.cpp" "$K/cpp-syn/common.cpp" \
  $(others "synthesis.cpp\|common.cpp") "$W/tools/audioio.cpp"
g++ -O2 -ffp-contract=off -w -I "$W/src" -I "$W/tools" -I "$K/cpp-ct" -o "$K/prof_ct" \
  "$K/cpp-ct/prof_main.cpp" "$K/cpp-ct/ct.cpp" \
  $(others "cheaptrick.cpp") "$W/tools/audioio.cpp"

echo "==== C++ Synthesis"
"$K/prof_syn" "$WAV"
echo "==== C++ CheapTrick"
"$K/prof_ct" "$WAV"

for t in syn ct; do
  cd "$K"
  rm -rf src out obj
  mkdir src
  cp "$GITHUB_WORKSPACE"/WorldNet/*.cs src/
  cp Prof.cs src/
  python3 "patch_$t.py" src
  cp "Program.$t.cs" Program.cs
  dotnet build -c Release -o out --nologo 2>&1 | grep -E "error|Error" || true
  echo "==== C# $t"
  dotnet out/csp.dll "$WAV"
done
