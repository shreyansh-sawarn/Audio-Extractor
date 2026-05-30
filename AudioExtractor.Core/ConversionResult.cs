namespace AudioExtractor.Core;

public sealed class ConversionResult
{
    public string InputPath { get; init; } = string.Empty;
    public string OutputPath { get; init; } = string.Empty;
    public bool Successful { get; init; }
    public int? ExitCode { get; init; }
    public string? ErrorMessage { get; init; }

    public static ConversionResult Failed(string inputPath, string outputPath, string errorMessage, int? exitCode = null)
    {
        return new ConversionResult
        {
            InputPath = inputPath,
            OutputPath = outputPath,
            Successful = false,
            ExitCode = exitCode,
            ErrorMessage = errorMessage
        };
    }
}
