#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")"

if [ ! -d world-src ]; then
  git clone --depth 1 https://github.com/mmorise/World.git world-src
fi

compiler="${CXX:-g++}"
input_wav="${1:-world-src/test/vaiueo2d.wav}"
output_dir="${2:-data}"

mkdir -p "$output_dir"

"$compiler" -O2 -ffp-contract=off \
  -I world-src/src -I world-src/tools -I . \
  -o worldref \
  main.cpp world-src/src/*.cpp world-src/tools/audioio.cpp world-src/tools/parameterio.cpp

./worldref "$input_wav" "$output_dir"
