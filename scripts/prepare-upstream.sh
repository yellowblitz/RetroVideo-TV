#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WORK_DIR="$ROOT_DIR/work"
RA_DIR="$WORK_DIR/RetroArch"
REVISION="$(tr -d '[:space:]' < "$ROOT_DIR/UPSTREAM_REVISION")"
UPSTREAM_URL="https://github.com/libretro/RetroArch.git"

mkdir -p "$WORK_DIR"

if [[ ! -d "$RA_DIR/.git" ]]; then
  git clone --filter=blob:none --no-checkout "$UPSTREAM_URL" "$RA_DIR"
fi

git -C "$RA_DIR" fetch --depth=1 origin "$REVISION"
git -C "$RA_DIR" checkout --detach --force "$REVISION"
git -C "$RA_DIR" reset --hard "$REVISION"
git -C "$RA_DIR" clean -fdx

shopt -s nullglob
patches=("$ROOT_DIR"/patches/*.patch)
for patch in "${patches[@]}"; do
  echo "Applying $(basename "$patch")"
  git -C "$RA_DIR" apply --whitespace=nowarn "$patch"
done

echo "RetroVideo TV source prepared at: $RA_DIR"
echo "RetroArch revision: $REVISION"
