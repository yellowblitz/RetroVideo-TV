# RetroVideo TV

RetroVideo TV is an Android TV video player built from RetroArch's media and shader stack, with the emulator/library features intentionally removed from the product experience.

## Goal

Open a local video, play it smoothly on Android TV, and apply real RetroArch Slang shader presets (`.slangp` + `.slang`) with live shader parameters.

## v0.1 scope

- Android TV / D-pad-first interface
- Local video playback
- FFmpeg media core
- Vulkan video output
- RetroArch Slang shader presets
- Multi-pass shader chains and LUT dependencies
- Live shader parameters
- Audio-track selection
- Subtitle-track selection
- Play/pause and seek controls
- AArch64 debug APK from GitHub Actions

## Architecture

This repository uses a patch-based fork model. CI downloads a pinned RetroArch upstream revision, applies RetroVideo TV patches/overlays, then builds the Android APK from RetroArch's `pkg/android/phoenix` project.

This keeps the project small while preserving direct compatibility with RetroArch's renderer and shader preset implementation.

## Upstream

The currently pinned RetroArch revision is stored in `UPSTREAM_REVISION`.

Run:

```bash
./scripts/prepare-upstream.sh
```

This creates `work/RetroArch`, checks out the pinned revision, and applies the patches under `patches/` in lexical order.

## Project status

Initial scaffold. The first milestone is:

> Open an MP4/MKV -> play -> choose a real `.slangp` preset -> apply it -> edit exposed shader parameters live.

## Licensing

RetroArch is licensed under GPLv3. RetroVideo TV is intended to remain GPLv3-compatible. Individual shader presets may have their own licenses; bundled shader content must be reviewed before distribution.
