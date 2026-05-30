# Audio Extractor

Audio Extractor is a small Windows WPF app for dropping video files and extracting their audio as MP3 files through ffmpeg.

## Features

- Drag-and-drop video input
- MP3 output at 256 kbps
- Per-file queued/converting/done/failed status
- Collision-safe output names
- Remembered target folder
- Clear error messages when conversion fails

## Supported Inputs

The app currently accepts:

- `.webm`
- `.mp4`
- `.mkv`
- `.mov`
- `.avi`
- `.m4v`

ffmpeg may support more formats than this list. Add extensions in `MainWindowViewModel.SupportedExtensions` if needed.

## Requirements

- Windows
- .NET 10 SDK
- ffmpeg available either:
  - on `PATH` as `ffmpeg`, or
  - bundled at `AudioExtractor/3rdparty/ffmpeg.exe` in the build output

## Build

From Windows PowerShell:

```powershell
dotnet build AudioExtractor.sln -m:1
```

This is a Windows-targeted WPF project. It may restore and compile from non-Windows environments with Windows targeting enabled, but running the app requires Windows.

## CLI

The shared converter is also available as a command-line app:

```bash
dotnet run --project AudioExtractor.Cli -- --output ./Output ./Videos
```

Use `--recursive` to scan child directories. The CLI accepts files or directories and writes collision-safe `.mp3` files to the output directory.

## Docker

Build the image:

```bash
docker build -t audio-extractor .
```

Run it with input and output folders mounted:

```bash
docker run --rm \
  -v "$PWD/input:/input:ro" \
  -v "$PWD/output:/output" \
  audio-extractor --recursive --output /output /input
```

Or use Docker Compose:

```bash
docker compose up --build
```

From Windows PowerShell, the helper scripts are the easiest path:

```powershell
.\scripts\build-docker.ps1
.\scripts\run-docker.ps1 -Recursive
```

By default, the run script reads from `.\input` and writes MP3 files to `.\output`.

## Project Structure

- `AudioExtractor.Core/MediaConverter.cs` runs ffmpeg and reports conversion results.
- `AudioExtractor.Cli/Program.cs` provides a Docker-friendly command-line entry point.
- `AudioExtractor/ViewModels/MainWindowViewModel.cs` owns app state, commands, filtering, and conversion flow.
- `AudioExtractor/Models/InputItemModel.cs` represents each dropped file.
- `AudioExtractor/AppSettings.cs` stores user settings in `%APPDATA%/AudioExtractor/settings.json`.
