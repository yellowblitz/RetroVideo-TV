# RetroVideo for Windows

RetroVideo for Windows is the desktop companion to the Android TV project. It uses RetroArch's Windows video/shader pipeline and the FFmpeg libretro media core so common desktop video formats can be opened while retaining real RetroArch Slang shader presets.

## First Windows milestone

- Self-contained `RetroVideo.exe` launcher
- Bundled RetroArch Windows x64 runtime
- Bundled `ffmpeg_libretro.dll` media core
- MP4, MKV, AVI, WMV, MOV, WebM, M4V, MPG/MPEG, TS/MTS/M2TS, FLV and OGV file picker
- Drag and drop video selection
- Real `.slangp` preset discovery from the bundled `shaders_slang` folder
- Custom `.slangp` preset selection
- Vulkan video output
- Fullscreen/windowed launch option

The packaged build does not require a separate RetroArch or .NET installation.

## Notes

Format support ultimately depends on the codecs enabled in the bundled FFmpeg libretro core. The file picker lists the common formats RetroVideo is intended to support, but no media player can guarantee every codec/profile stored inside every container.

The first Windows version is deliberately a thin player launcher. Playback is handled by RetroArch's media core so shader compatibility remains identical to RetroArch rather than being approximated by another renderer.
