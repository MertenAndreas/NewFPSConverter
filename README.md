# NewFPS Converter

NewFPS Converter is a Windows desktop tool for converting video files to a
specified frame rate. It is designed for workflows where a fixed output frame
rate is required, including video preparation for e:cue Sympholight projects.

Current release: `0.1.0.4`

## Features

- Convert single videos or batches of videos to a selected target FPS.
- Package converted output for MediaPackage-based workflows.
- Use bundled FFmpeg tools for local conversion.
- Detect available GPU acceleration options where supported.
- Run conversions from the terminal with proper output and exit codes.
- Install with the published Windows setup package (self-contained, no separate .NET installation required).

## Download

The latest Windows installer is available on the
[Releases](https://github.com/thomsenle/NewFPSConverter/releases) page.

## Project Page

A compact description page is available from the `docs/` folder and can be used
with GitHub Pages.

## Build

Requirements:

- Windows
- .NET SDK with Windows desktop workload support
- Inno Setup 6.x for building the installer

Build the application:

```powershell
dotnet publish FramerateConverter\NewFPSConverter.csproj -c Release -r win-x64 --self-contained true
```

Build the installer with Inno Setup using `installer.iss`.

## Command Line

Single-file conversion uses source and target files:

```powershell
NewFPSConverter.exe --source "input.mp4" --target "output.mpkg" --frames-per-second 30
```

If `--target` has no extension, `.mpkg` is appended. If `--target` is omitted,
the output defaults to the source filename with the `.mpkg` extension.

Batch conversion uses source and target folders:

```powershell
NewFPSConverter.exe --batch --source-dir "C:\Videos\Input" --target-dir "C:\Videos\Output" --frames-per-second 30
```

Batch output defaults to MediaPackage (`.mpkg`). Use `--overwrite` to replace
existing target files.

### Command Line Options

| Option | Short | Description |
|---|---|---|
| `--batch` | `-b` | Enable batch behavior: run once, then terminate automatically. |
| `--source <file>` | `-s` | Input video file path. |
| `--source-dir <dir>` | `-d` | Source directory for batch processing multiple videos. |
| `--target <file>` | `-t` | Output video or container file (`.mpkg`, `.mp4`, `.h264`, etc.). |
| `--target-dir <dir>` | `-O` | Target directory for batch output files. |
| `--frames-per-second <fps>` | `-f` | Target framerate (allowed: 10 - 200 FPS). |
| `--key-frames-only` | `-k` | Output video will consist only of key frames (I-frames). |
| `--no-flicker` | `-n` | Reduce flicker by adjusting framerate. |
| `--min-frame-rate` | `-m` | Treat target FPS as minimum (do not reduce FPS). |
| `--overwrite` | `-o` | Overwrite existing target file(s) without prompting. |
| `--audio` | `-a` | Keep original audio stream(s) if present. |
| `--sl-compatibility` | `-l` | Enable Sympholight compatible H.264 output settings. |
| `--gpu-mediapackage` | `-p` | Allow GPU hardware encoding for MediaPackage/H.264. |
| `--force-width <px>` | `-W` | Enforce target video width in pixels. |
| `--force-height <px>` | `-H` | Enforce target video height in pixels. |
| `--bitrate <kbits>` | `-r` | Target bitrate in kbit/s (0 = auto). |
| `--gpu <type>` | `-g` | GPU encoder: `auto`, `nvidia`, `amd`, `intel`, or `none` (CPU only). |
| `--parallel-jobs <n>` | `-j` | Number of parallel encoding processes (0 = auto). |
| `--no-gui` | | Run task completely in background without GUI window (aliases: `--headless`, `--background`). |
| `--help` | | Display command line help screen. |
| `--version` | | Display version information. |

### Return Codes

| Code | Meaning |
|---|---|
| `0` | OK (Conversion completed successfully) |
| `1` | Target file already exists and cannot be overwritten |
| `2` | Process was cancelled |
| `3` | Target framerate does not differ enough from source framerate |
| `4` | Target framerate is illegal (allowed: 10 - 200 FPS) |
| `5` | Command line parameter error / invalid arguments |
| `6` | Input file error (not a valid video file) |
| `7` | GPL v2 license was not accepted |
| `8` | Target file is corrupt |
| `100` | Generic / unknown error |

### Background / Headless Execution (No GUI)

To run conversions completely in the background without opening any GUI window:

```powershell
NewFPSConverter.exe --source "input.mp4" --target "output.mpkg" --frames-per-second 30 --no-gui
```

or for batch processing:

```powershell
NewFPSConverter.exe --batch --source-dir "C:\Videos\Input" --target-dir "C:\Videos\Output" --frames-per-second 30 --no-gui
```

## Background

Created by Thomas Sebastian. This project is derived from the Free Video FPS
Converter code published at:

https://github.com/freevideofps/freevideofpsconverter/releases

Please use GitHub issues to request features or submit bug reports.

## License

See the [LICENSE](./LICENSE) file for more information.
