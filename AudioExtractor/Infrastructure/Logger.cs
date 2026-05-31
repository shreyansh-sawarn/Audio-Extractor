using System;
using System.IO;

namespace AudioExtractor.Infrastructure;

public static class Logger
{
    private static readonly object LockObj = new();

    // Defaults to the default Music/AudioExtractor/logs directory
    public static string LogDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
        "AudioExtractor",
        "logs");

    private static string LogFilePath => Path.Combine(LogDirectory, $"activity_{DateTime.Now:yyyy_MM_dd}.log");

    public static void Log(string message, string level = "INFO")
    {
        try
        {
            lock (LockObj)
            {
                Directory.CreateDirectory(LogDirectory);
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var line = $"[{timestamp}] [{level}] {message}";
                File.AppendAllText(LogFilePath, line + Environment.NewLine);
            }
        }
        catch
        {
            // Fail silently to avoid throwing exceptions from logger
        }
    }

    public static void LogError(string message, Exception? ex = null)
    {
        var msg = ex != null ? $"{message} | Exception: {ex.Message}\n{ex.StackTrace}" : message;
        Log(msg, "ERROR");
    }

    public static void PurgeOldLogs()
    {
        try
        {
            lock (LockObj)
            {
                if (!Directory.Exists(LogDirectory)) return;

                var files = Directory.GetFiles(LogDirectory, "activity_*.log");
                var cutoff = DateTime.Now.AddDays(-30);

                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.LastWriteTime < cutoff)
                    {
                        File.Delete(file);
                    }
                }
            }
        }
        catch
        {
            // Fail silently
        }
    }
}
