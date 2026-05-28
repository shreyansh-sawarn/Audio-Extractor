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
dotnet build AudioExtractor.sln
```

This is a Windows-targeted WPF project. It may restore and compile from non-Windows environments with Windows targeting enabled, but running the app requires Windows.

## Project Structure

- `AudioExtractor/MediaConverter.cs` runs ffmpeg and reports conversion results.
- `AudioExtractor/ViewModels/MainWindowViewModel.cs` owns app state, commands, filtering, and conversion flow.
- `AudioExtractor/Models/InputItemModel.cs` represents each dropped file.
- `AudioExtractor/AppSettings.cs` stores user settings in `%APPDATA%/AudioExtractor/settings.json`.
