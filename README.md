# Audio Extractor

Audio Extractor is a versatile, high-performance Windows and container-ready suite designed to extract high-quality audio streams from video files using the power of FFmpeg. It provides both a premium **WPF Desktop Graphical User Interface (GUI)** for Windows users and a lightweight, Docker-ready **Command-Line Interface (CLI)** for automated and bulk processing workflows.

---

## What Was Implemented (Features & Possibilities)

The application has been enhanced with advanced media conversion capabilities, visual controls, and workflow automations:

### 1. WPF Desktop Application (GUI)
*   **Persistent Drag-and-Drop Queue:** Drop video files directly onto the interface. A persistent drop-zone overlay remains available at the bottom of the window so you can easily append more files at any time.
*   **Format & Quality Selection:** 
    *   **Output Formats:** Select between `MP3`, `M4A`, `WAV`, or `FLAC`.
    *   **Bitrate Customization:** Set audio quality to `128k`, `256k`, or `320k`.
*   **Time-Range Cropping (Trimming):** Specify exact `Start Time` and `End Time` (in `HH:mm:ss` format) for each queued file to extract only a specific segment instead of the whole video.
*   **Advanced Audio Settings:**
    *   **Normalize Volume:** Smooth out audio levels automatically.
    *   **Force Mono:** Convert stereo or multi-channel audio tracks to single-channel (Mono).
*   **Flexible Batch Concurrency:** Choose between `1` to `4` parallel conversion tasks to leverage multi-core processors.
*   **Naming Templates:** Configure custom naming rules for output files using patterns like `[Name]` (e.g., prepend/append text around the original file name).
*   **Sleek Theme Selection:** Instantly toggle between a premium **Dark Mode** and a clean **Light Mode**.
*   **Post-Conversion Actions:** Program the app to run an automated action upon batch completion:
    *   *Play Sound* (notification chime).
    *   *Open Folder* (automatically opens the output directory in File Explorer).
*   **Queue Management:** Add files via file picker, remove individual files, clear completed jobs, or clear the entire queue.
*   **Actionable Progress Tracking:** Real-time progress bars per file, live status tags (`Queued`, `Converting`, `Done`, `Failed`, `Cancelled`), and direct buttons on completed items to **Play** the audio or **Open** its location in File Explorer.
*   **Robust Error Classification:** If a conversion fails, the app inspects the FFmpeg stream to provide clear, human-readable troubleshooting tips (e.g., "Disk Full", "Access Denied", "Corrupted Input File", "Missing FFmpeg", "Invalid Parameters").
*   **Persistent Preferences:** Output paths, theme, bitrate, parallel limit, and conversion configurations are automatically saved locally in `%APPDATA%/AudioExtractor/settings.json`.

### 2. Command-Line Interface (CLI)
*   **Bulk Directory Scanning:** Point the CLI to single files or entire directories.
*   **Recursive Operations:** Use the `-r` flag to recursively walk subdirectory trees and extract audio from all discovered videos.
*   **Custom FFmpeg Paths:** Override the default environment path with a custom executable.
*   **Docker Integration:** Optimized to run in headless container environments.

### 3. Docker Deployment
*   **Containerized Processing:** Extract audio on non-Windows platforms (macOS, Linux) using Docker.
*   **Ready-to-use Compose and Scripts:** Includes `docker-compose` and PowerShell helper scripts to make containerized conversions trivial.

---

## Supported Input Formats
The application filters and accepts the following common video containers out-of-the-box:
*   `.mp4`, `.mkv`, `.webm`, `.mov`, `.avi`, `.m4v`

*(Tip: You can expand supported formats by adding extensions to `SupportedExtensions` in `MainWindowViewModel.cs`)*

---

## Requirements

### Local Build and Run
*   **OS:** Windows 10/11 (required for the WPF GUI; CLI is cross-platform).
*   **SDK:** [.NET 10 SDK](https://dotnet.microsoft.com/download)
*   **FFmpeg:** Must be available either:
    *   On your system `PATH` (as `ffmpeg`), or
    *   Placed directly inside the build output directory under `AudioExtractor/3rdparty/ffmpeg.exe`.

### Containerized Environment
*   [Docker Desktop](https://www.docker.com/products/docker-desktop/) (WSL 2 backend recommended).

---

## How to Easily Run the Applications

### 1. Running the WPF Graphical User Interface (GUI)

To compile and launch the Desktop GUI:

1.  Open **Windows PowerShell** or **Command Prompt** in the project root folder.
2.  Build the solution:
    ```powershell
    dotnet build AudioExtractor.sln
    ```
3.  Run the WPF application project:
    ```powershell
    dotnet run --project AudioExtractor
    ```

---

### 2. Running the Command-Line Interface (CLI)

The CLI tool lets you run conversions quickly from your terminal:

```bash
dotnet run --project AudioExtractor.Cli -- [options] <input-paths>
```

#### Options:
*   `-o, --output <directory>`: Target directory for `.mp3` files (defaults to `./Output`).
*   `-r, --recursive`: Recursively scan directories for supported videos.
*   `--ffmpeg <path>`: Specify the absolute path to your FFmpeg binary.

#### Examples:
```bash
# Extract audio from a single video file
dotnet run --project AudioExtractor.Cli -- ./Videos/lecture.mp4

# Recursively extract audio from all videos inside a folder and output them to a specific folder
dotnet run --project AudioExtractor.Cli -- --recursive --output ./MyAudioFiles ./Videos
```

---

### 3. Running with Docker

Docker allows you to run the CLI tool without needing .NET or FFmpeg installed on your machine.

#### Using PowerShell Helper Scripts (Easiest)
We provide two helper scripts inside the `scripts` folder to streamline building and running:

1.  **Build the Docker Image:**
    ```powershell
    .\scripts\build-docker.ps1
    ```
2.  **Run the Container:**
    ```powershell
    .\scripts\run-docker.ps1 -Recursive
    ```
    *By default, this script reads video files from `.\input` in your project folder, runs the extraction recursively, and writes the output audio files directly into `.\output` on your host machine.*

#### Manual Docker Commands
If you prefer running raw Docker commands:

1.  **Build the Image:**
    ```bash
    docker build -t audio-extractor .
    ```
2.  **Run the Container (Mounting Input & Output):**
    ```bash
    docker run --rm \
      -v "$PWD/input:/input:ro" \
      -v "$PWD/output:/output" \
      audio-extractor --recursive --output /output /input
    ```

#### Using Docker Compose
Simply run:
```bash
docker compose up --build
```
This mounts the local `./input` and `./output` directories to perform bulk conversions automatically.

---

## Project Directory Map

*   [`AudioExtractor.Core/`](file:///c:/Users/shrey/OneDrive/Documents/Repos/Audio-Extractor/AudioExtractor.Core): Core engine containing `MediaConverter.cs` which manages the FFmpeg process, reports progress, and formats output filenames.
*   [`AudioExtractor.Cli/`](file:///c:/Users/shrey/OneDrive/Documents/Repos/Audio-Extractor/AudioExtractor.Cli): Command-line entry point and argument parsing.
*   [`AudioExtractor/`](file:///c:/Users/shrey/OneDrive/Documents/Repos/Audio-Extractor/AudioExtractor): Desktop UI application (WPF).
    *   [`ViewModels/MainWindowViewModel.cs`](file:///c:/Users/shrey/OneDrive/Documents/Repos/Audio-Extractor/AudioExtractor/ViewModels/MainWindowViewModel.cs): Main view model orchestrating queue updates, parallel batch logic, error parsing, and options.
    *   [`MainWindow.xaml`](file:///c:/Users/shrey/OneDrive/Documents/Repos/Audio-Extractor/AudioExtractor/MainWindow.xaml): Application UI layout (styles, overlays, progress bars, settings panels).
*   [`scripts/`](file:///c:/Users/shrey/OneDrive/Documents/Repos/Audio-Extractor/scripts): Native scripts for building and executing Docker runs.

---

## CI/CD & Repository Automation

The repository utilizes GitHub Actions and Dependabot to automate builds, code format checks, security scans, releases, and dependency management:

1.  **.NET Build & Release (`desktop-build-release.yml`):**
    *   Runs on every pull request and push to the `master` branch. Supports manual trigger (`workflow_dispatch`).
    *   Builds, packages, and zips self-contained `win-x64` executables for both the WPF Desktop GUI and CLI applications.
    *   Upon tagging a version (`v*`) or manual release request, it builds the release binaries and publishes them directly to GitHub Releases.
2.  **Docker Build & Publish (`docker-publish.yml`):**
    *   Triggers on PRs and commits targeting the `master` branch, as well as manual runs.
    *   Automatically builds and pushes the production-ready CLI Docker image to the GitHub Container Registry (GHCR) at `ghcr.io/your-username/audio-extractor`.
3.  **Code Formatting Check (`code-format.yml`):**
    *   Triggered on commits and PRs to `master` to verify C# code formatting (`dotnet format`) and ensure stylistic consistency.
4.  **CodeQL Security Analysis (`codeql-analysis.yml`):**
    *   Runs automated static analysis security scans (SAST) on a weekly schedule (every Monday) and on pushes/PRs to check for potential vulnerabilities and bugs.
5.  **Dependabot updates (`dependabot.yml`):**
    *   Monitors NuGet dependencies and GitHub Actions weekly, automatically creating pull requests when secure or updated library versions are released.
