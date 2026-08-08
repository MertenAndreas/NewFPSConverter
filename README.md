# NewFPS Converter

NewFPS Converter is a Windows desktop tool for converting video files to a
specified frame rate. It is designed for workflows where a fixed output frame
rate is required, including video preparation for e:cue Sympholight projects.

## Features

- Convert single videos or batches of videos to a selected target FPS.
- Package converted output for MediaPackage-based workflows.
- Use bundled FFmpeg tools for local conversion.
- Detect available GPU acceleration options where supported.
- Install with the published Windows setup package.

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
dotnet publish FramerateConverter\NewFPSConverter.csproj -c Release
```

Build the installer with Inno Setup using `installer.iss`.

## Background

Created by Thomas Sebastian. This project is derived from the Free Video FPS
Converter code published at:

https://github.com/freevideofps/freevideofpsconverter/releases

Please use GitHub issues to request features or submit bug reports.

## License

See the [LICENSE](./LICENSE) file for more information.
