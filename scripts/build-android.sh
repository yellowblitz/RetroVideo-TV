#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
bash "$ROOT_DIR/scripts/prepare-upstream.sh"

cd "$ROOT_DIR/work/RetroArch/pkg/android/phoenix"
./gradlew assembleAarch64Debug

APK_DIR="$ROOT_DIR/work/RetroArch/pkg/android/phoenix/build/outputs/apk/aarch64/debug"
echo "Build complete. APKs:"
find "$APK_DIR" -maxdepth 1 -type f -name '*.apk' -print
