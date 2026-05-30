using System.Diagnostics;
using System.Text;

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

    public async Task<ConversionResult> ConvertFileAsync(string inputPath, CancellationToken cancellationToken = default)
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

        var outputPath = CreateUniqueOutputPath(inputPath);
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
        startInfo.ArgumentList.Add("-c:a");
        startInfo.ArgumentList.Add("libmp3lame");
        startInfo.ArgumentList.Add("-b:a");
        startInfo.ArgumentList.Add("256k");
        startInfo.ArgumentList.Add(outputPath);

        using var ffmpeg = new Process { StartInfo = startInfo };
        var output = new StringBuilder();

        ffmpeg.OutputDataReceived += (_, e) => AppendLine(output, e.Data);
        ffmpeg.ErrorDataReceived += (_, e) => AppendLine(output, e.Data);

        try
        {
            ffmpeg.Start();
            ffmpeg.BeginOutputReadLine();
            ffmpeg.BeginErrorReadLine();
            await ffmpeg.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            TryKill(ffmpeg);
            throw;
        }
        catch (Exception ex)
        {
            return ConversionResult.Failed(inputPath, outputPath, ex.Message);
        }

        if (ffmpeg.ExitCode == 0 && File.Exists(outputPath))
        {
            return new ConversionResult
            {
                InputPath = inputPath,
                OutputPath = outputPath,
                Successful = true,
                ExitCode = ffmpeg.ExitCode
            };
        }

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

    private string CreateUniqueOutputPath(string inputPath)
    {
        var baseName = Path.GetFileNameWithoutExtension(inputPath);
        var outputPath = Path.Combine(_targetDirectory, $"{baseName}.mp3");
        var index = 1;

        while (File.Exists(outputPath))
        {
            outputPath = Path.Combine(_targetDirectory, $"{baseName} ({index}).mp3");
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
}
