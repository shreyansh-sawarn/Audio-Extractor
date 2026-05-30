using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace AudioExtractor.Core;

public sealed class MediaConverter
{
    private readonly string _targetDirectory;
    private readonly string _ffmpegPath;

    public MediaConverter(string targetDirectory, string? ffmpegPath = null)
    {
        if (string.IsNullOrWhiteSpace(targetDirectory))
            throw new ArgumentException("Target directory is required.", nameof(targetDirectory));

        _targetDirectory = targetDirectory;
        _ffmpegPath = string.IsNullOrWhiteSpace(ffmpegPath) ? ResolveFfmpegPath() : ffmpegPath;
    }

    public bool IsFfmpegAvailable => File.Exists(_ffmpegPath) || _ffmpegPath == "ffmpeg";

    public async Task<ConversionResult> ConvertFileAsync(
        string inputPath, 
        string format = "mp3", 
        string bitrate = "256k", 
        bool isMono = false, 
        bool normalize = false, 
        IProgress<double>? progress = null, 
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(inputPath))
        {
            return ConversionResult.Failed(inputPath, string.Empty, "Input file no longer exists.");
        }

        Directory.CreateDirectory(_targetDirectory);

        if (!IsFfmpegAvailable)
        {
            return ConversionResult.Failed(inputPath, string.Empty, $"ffmpeg was not found at '{_ffmpegPath}'.");
        }

        var ext = format.ToLowerInvariant();
        var outputPath = CreateUniqueOutputPath(inputPath, ext);
        var startInfo = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        startInfo.ArgumentList.Add("-hide_banner");
        startInfo.ArgumentList.Add("-y");
        startInfo.ArgumentList.Add("-i");
        startInfo.ArgumentList.Add(inputPath);
        startInfo.ArgumentList.Add("-vn");

        switch (ext)
        {
            case "m4a":
                startInfo.ArgumentList.Add("-c:a");
                startInfo.ArgumentList.Add("aac");
                startInfo.ArgumentList.Add("-b:a");
                startInfo.ArgumentList.Add(bitrate);
                break;
            case "wav":
                startInfo.ArgumentList.Add("-c:a");
                startInfo.ArgumentList.Add("pcm_s16le");
                break;
            case "flac":
                startInfo.ArgumentList.Add("-c:a");
                startInfo.ArgumentList.Add("flac");
                break;
            case "mp3":
            default:
                startInfo.ArgumentList.Add("-c:a");
                startInfo.ArgumentList.Add("libmp3lame");
                startInfo.ArgumentList.Add("-b:a");
                startInfo.ArgumentList.Add(bitrate);
                break;
        }

        if (isMono)
        {
            startInfo.ArgumentList.Add("-ac");
            startInfo.ArgumentList.Add("1");
        }

        if (normalize)
        {
            startInfo.ArgumentList.Add("-filter:a");
            startInfo.ArgumentList.Add("loudnorm");
        }

        startInfo.ArgumentList.Add(outputPath);

        using var ffmpeg = new Process { StartInfo = startInfo };
        var output = new StringBuilder();

        double totalDurationSeconds = 0;
        void HandleLine(string? line)
        {
            if (string.IsNullOrEmpty(line)) return;
            AppendLine(output, line);

            // Parse total duration of source video
            if (line.Contains("Duration:"))
            {
                var match = Regex.Match(line, @"Duration:\s*(\d+):(\d+):(\d+)(?:\.(\d+))?");
                if (match.Success)
                {
                    var hours = double.Parse(match.Groups[1].Value);
                    var minutes = double.Parse(match.Groups[2].Value);
                    var seconds = double.Parse(match.Groups[3].Value);
                    double centiseconds = 0;
                    if (match.Groups.Count > 4 && match.Groups[4].Success && !string.IsNullOrEmpty(match.Groups[4].Value))
                    {
                        var valStr = match.Groups[4].Value;
                        centiseconds = double.Parse(valStr) * Math.Pow(10, 2 - valStr.Length);
                    }
                    totalDurationSeconds = hours * 3600 + minutes * 60 + seconds + centiseconds / 100;
                }
            }
            // Parse current conversion timestamp to calculate progress percentage
            else if (line.Contains("time=") && totalDurationSeconds > 0)
            {
                var match = Regex.Match(line, @"time=\s*(\d+):(\d+):(\d+)(?:\.(\d+))?");
                if (match.Success)
                {
                    var hours = double.Parse(match.Groups[1].Value);
                    var minutes = double.Parse(match.Groups[2].Value);
                    var seconds = double.Parse(match.Groups[3].Value);
                    double centiseconds = 0;
                    if (match.Groups.Count > 4 && match.Groups[4].Success && !string.IsNullOrEmpty(match.Groups[4].Value))
                    {
                        var valStr = match.Groups[4].Value;
                        centiseconds = double.Parse(valStr) * Math.Pow(10, 2 - valStr.Length);
                    }
                    var currentSeconds = hours * 3600 + minutes * 60 + seconds + centiseconds / 100;
                    var percentage = (currentSeconds / totalDurationSeconds) * 100;
                    if (percentage > 100) percentage = 100;
                    if (percentage < 0) percentage = 0;
                    progress?.Report(percentage);
                }
            }
        }

        ffmpeg.OutputDataReceived += (_, e) => HandleLine(e.Data);
        ffmpeg.ErrorDataReceived += (_, e) => HandleLine(e.Data);

        try
        {
            ffmpeg.Start();
            ffmpeg.BeginOutputReadLine();
            ffmpeg.BeginErrorReadLine();

            using (cancellationToken.Register(() => TryKill(ffmpeg)))
            {
                await ffmpeg.WaitForExitAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            TryKill(ffmpeg);
            TryDeleteFile(outputPath);
            throw;
        }
        catch (Exception ex)
        {
            TryDeleteFile(outputPath);
            return ConversionResult.Failed(inputPath, outputPath, ex.Message);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            TryDeleteFile(outputPath);
            return ConversionResult.Failed(inputPath, outputPath, "Operation cancelled.", -1);
        }

        if (ffmpeg.ExitCode == 0 && File.Exists(outputPath))
        {
            progress?.Report(100);
            return new ConversionResult
            {
                InputPath = inputPath,
                OutputPath = outputPath,
                Successful = true,
                ExitCode = ffmpeg.ExitCode
            };
        }

        TryDeleteFile(outputPath);
        return ConversionResult.Failed(
            inputPath,
            outputPath,
            GetLastMeaningfulLine(output.ToString()) ?? $"ffmpeg exited with code {ffmpeg.ExitCode}.",
            ffmpeg.ExitCode);
    }

    private static string ResolveFfmpegPath()
    {
        var bundledPath = Path.Combine(AppContext.BaseDirectory, "3rdparty", "ffmpeg.exe");
        return File.Exists(bundledPath) ? bundledPath : "ffmpeg";
    }

    private string CreateUniqueOutputPath(string inputPath, string ext)
    {
        var baseName = Path.GetFileNameWithoutExtension(inputPath);
        var outputPath = Path.Combine(_targetDirectory, $"{baseName}.{ext}");
        var index = 1;

        while (File.Exists(outputPath))
        {
            outputPath = Path.Combine(_targetDirectory, $"{baseName} ({index}).{ext}");
            index++;
        }

        return outputPath;
    }

    private static void AppendLine(StringBuilder builder, string? line)
    {
        if (!string.IsNullOrWhiteSpace(line))
            builder.AppendLine(line);
    }

    private static string? GetLastMeaningfulLine(string text)
    {
        var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        return lines.Length == 0 ? null : lines[^1];
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
        }
    }
}
