# RetroVideo for Windows

RetroVideo for Windows is the desktop companion to the Android TV project. It uses RetroArch's Windows built-in media player and native video/shader pipeline so common desktop video formats can be opened while retaining real RetroArch Slang shader presets.

## First Windows milestone

- Self-contained `RetroVideo.exe` launcher
- Bundled RetroArch 1.22.2 Windows x64 runtime
- RetroArch built-in media playback enabled automatically
- MP4, MKV, AVI, WMV, MOV, WebM, M4V, MPG/MPEG, TS/MTS/M2TS, FLV and OGV file picker
- Drag and drop video selection
- Real `.slangp` preset discovery from the bundled `shaders_slang` folder
- Custom `.slangp` preset selection
- Vulkan video output
- Fullscreen/windowed launch option

The packaged build does not require a separate RetroArch or .NET installation.

## Notes

Format support ultimately depends on the FFmpeg/media capabilities compiled into the bundled RetroArch Windows build. The file picker lists the common formats RetroVideo is intended to support, but no media player can guarantee every codec/profile stored inside every container.

The first Windows version is deliberately a thin player launcher. Playback stays inside RetroArch so shader compatibility remains native rather than being approximated by another renderer.
