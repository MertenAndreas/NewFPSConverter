# NewFPS Converter

NewFPS Converter is a Windows desktop tool for converting video files to a
specified frame rate. It is designed for workflows where a fixed output frame
rate is required, including video preparation for e:cue Sympholight projects.

Current release: `0.1.0.3`

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

## Background

Created by Thomas Sebastian. This project is derived from the Free Video FPS
Converter code published at:

https://github.com/freevideofps/freevideofpsconverter/releases

Please use GitHub issues to request features or submit bug reports.

## License

See the [LICENSE](./LICENSE) file for more information.
