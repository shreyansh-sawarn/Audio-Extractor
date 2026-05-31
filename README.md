# Audio Extractor

Audio Extractor is a versatile, high-performance suite designed to extract high-quality audio streams from video files using the power of FFmpeg. It provides:
1. A premium, modern **Tauri Cross-Platform Desktop Companion (Windows, macOS, Linux)** built with React, Vite, and TypeScript.
2. A premium **WPF Desktop Graphical User Interface (GUI)** for native Windows environments.
3. A lightweight, Docker-ready **Command-Line Interface (CLI)** for automated and bulk processing workflows.

---

## What Was Implemented (Features & Possibilities)

The application has been enhanced with advanced media conversion capabilities, visual controls, and workflow automations:

### 1. Tauri Cross-Platform Desktop Companion (GUI)
*   **WASM Browser Fallback:** Converts video files under 150MB entirely inside the web browser view using FFmpeg WASM when running in web mode, requiring zero local installations.
*   **System FFmpeg Integration:** Detects and triggers local system-installed FFmpeg binaries for files larger than 150MB or when running inside the desktop shell.
*   **Full-Viewport Drag & Drop:** Drop videos anywhere on the window. Standard HTML5 file-drop APIs cleanly handle list reordering and queue loading.
*   **Sequential/Parallel Conversion Queues:** Runs multiple extractions concurrently or sequentially according to preference settings.
*   **Sleek Modern UI:** Vibrant HSL colors, responsive design, dark mode preference, and visual indicators.

### 2. WPF Desktop Application (GUI)
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

### 3. Command-Line Interface (CLI)
*   **Bulk Directory Scanning:** Point the CLI to single files or entire directories.
*   **Recursive Operations:** Use the `-r` flag to recursively walk subdirectory trees and extract audio from all discovered videos.
*   **Custom FFmpeg Paths:** Override the default environment path with a custom executable.
*   **Docker Integration:** Optimized to run in headless container environments.

### 4. Docker Deployment
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
*   **OS:** Windows 10/11 (required for native WPF GUI), macOS/Linux/Windows (supported for Tauri Desktop).
*   **SDK:** [.NET 10 SDK](https://dotnet.microsoft.com/download)
*   **Node.js:** v18+ (needed for compiling the Tauri frontend app).
*   **Rust:** Stable Toolchain (needed for compiling the Tauri desktop backend binary).
*   **FFmpeg:** Must be available either:
    *   On your system `PATH` (as `ffmpeg`), or
    *   Placed directly inside the build output directory under `AudioExtractor/3rdparty/ffmpeg.exe`.

### Containerized Environment
*   [Docker Desktop](https://www.docker.com/products/docker-desktop/) (WSL 2 backend recommended).

---

## How to Easily Run the Applications

### 1. Running the Tauri Cross-Platform Companion (GUI)

To run the Tauri Desktop Application in development mode:

1. Navigate to the Tauri directory:
   ```bash
   cd AudioExtractor.Tauri
   ```
2. Install the required Node dependencies:
   ```bash
   npm install
   ```
3. Run the development server and desktop app shell:
   ```bash
   npx @tauri-apps/cli dev
   ```

To compile a production build of the Tauri Desktop application:
```bash
npx @tauri-apps/cli build
```

---

### 2. Running the WPF Graphical User Interface (GUI)

To compile and launch the Desktop GUI:

1. Open **Windows PowerShell** or **Command Prompt** in the project root folder.
2. Build the solution:
   ```powershell
   dotnet build AudioExtractor.sln
   ```
3. Run the WPF application project:
   ```powershell
   dotnet run --project AudioExtractor
   ```

---

### 3. Running the Command-Line Interface (CLI)

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

### 4. Running with Docker

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

*   [`AudioExtractor.Core/`](file:///C:/Users/shrey/Documents/Repos/Audio-Extractor/AudioExtractor.Core): Core engine containing `MediaConverter.cs` which manages the FFmpeg process, reports progress, and formats output filenames.
*   [`AudioExtractor.Cli/`](file:///C:/Users/shrey/Documents/Repos/Audio-Extractor/AudioExtractor.Cli): Command-line entry point and argument parsing.
*   [`AudioExtractor/`](file:///C:/Users/shrey/Documents/Repos/Audio-Extractor/AudioExtractor): Desktop UI application (WPF).
*   [`AudioExtractor.Tauri/`](file:///C:/Users/shrey/Documents/Repos/Audio-Extractor/AudioExtractor.Tauri): Cross-platform desktop companion application using Tauri v2, React, Vite, and TS.
*   [`scripts/`](file:///C:/Users/shrey/Documents/Repos/Audio-Extractor/scripts): Native scripts for building and executing Docker runs.

---

## CI/CD & Repository Automation

The repository utilizes GitHub Actions and Dependabot to automate builds, code format checks, security scans, releases, and dependency management:

1.  **.NET & Tauri Build & Release (`desktop-build-release.yml`):**
    *   Runs on every pull request and push to the `master` branch. Supports manual trigger (`workflow_dispatch`).
    *   Builds, packages, and zips self-contained `win-x64` executables for both the WPF Desktop GUI and CLI applications.
    *   Runs cross-platform Tauri builder matrix (macOS, Linux, Windows) to build native installer assets.
    *   Upon tagging a version (`v*`) or manual release request, it builds the release binaries and publishes them directly to GitHub Releases.
    *   Includes a failsafe ancestry check verifying that releases can only be generated from tags cut off the `master` branch.
2.  **Docker Build & Publish (`docker-publish.yml`):**
    *   Triggers on PRs and commits targeting the `master` branch, as well as manual runs.
    *   Automatically builds and pushes the production-ready CLI Docker image to the GitHub Container Registry (GHCR) at `ghcr.io/your-username/audio-extractor`.
    *   Includes a failsafe ancestry check verifying that publication packages are only generated from tags cut off the `master` branch.
3.  **Code Formatting Check (`code-format.yml`):**
    *   Triggered on commits and PRs to `master` to verify C# code formatting (`dotnet format`) and ensure stylistic consistency.
4.  **CodeQL Security Analysis (`codeql-analysis.yml`):**
    *   Runs automated static analysis security scans (SAST) on a weekly schedule (every Monday) and on pushes/PRs to check for potential vulnerabilities and bugs.
5.  **Dependabot updates (`dependabot.yml`):**
    *   Monitors NuGet dependencies and GitHub Actions weekly, automatically creating pull requests when secure or updated library versions are released.
