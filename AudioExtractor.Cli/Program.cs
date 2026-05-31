using AudioExtractor.Core;

var options = CliOptions.Parse(args);

if (options.ShowHelp)
{
    PrintHelp();
    return options.HasError ? 1 : 0;
}

Directory.CreateDirectory(options.OutputDirectory);

var converter = new MediaConverter(options.OutputDirectory, options.FfmpegPath);
var inputs = ExpandInputs(options.InputPaths, options.Recursive).ToArray();

if (inputs.Length == 0)
{
    Console.Error.WriteLine("No supported input files were found.");
    return 1;
}

var failed = 0;

foreach (var input in inputs)
{
    Console.WriteLine($"Converting: {input}");
    var result = await converter.ConvertFileAsync(input);

    if (result.Successful)
    {
        Console.WriteLine($"Created: {result.OutputPath}");
        continue;
    }

    failed++;
    Console.Error.WriteLine($"Failed: {input}");
    Console.Error.WriteLine(result.ErrorMessage);
}

Console.WriteLine(failed == 0
    ? $"Finished {inputs.Length} file(s)."
    : $"Finished {inputs.Length} file(s) with {failed} failure(s).");

return failed == 0 ? 0 : 1;

static IEnumerable<string> ExpandInputs(IEnumerable<string> paths, bool recursive)
{
    var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    foreach (var path in paths)
    {
        if (File.Exists(path) && IsSupported(path) && seen.Add(Path.GetFullPath(path)))
        {
            yield return path;
            continue;
        }

        if (!Directory.Exists(path))
            continue;

        foreach (var filePath in Directory.EnumerateFiles(path, "*.*", searchOption)
                     .Where(IsSupported)
                     .OrderBy(filePath => filePath, StringComparer.OrdinalIgnoreCase))
        {
            if (seen.Add(Path.GetFullPath(filePath)))
                yield return filePath;
        }
    }
}

static bool IsSupported(string path)
{
    var extension = Path.GetExtension(path).TrimStart('.');
    return CliOptions.SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
}

static void PrintHelp()
{
    Console.WriteLine("""
    Audio Extractor CLI

    Usage:
      audio-extractor [options] <file-or-directory> [...]

    Options:
      -o, --output <directory>   Output directory. Defaults to /output in Docker, otherwise ./Output.
      -r, --recursive            Include supported files in child directories.
      --ffmpeg <path>            Path to ffmpeg. Defaults to ffmpeg on PATH.
      -h, --help                 Show help.

    Supported inputs:
      .webm, .mp4, .mkv, .mov, .avi, .m4v
    """);
}

internal sealed class CliOptions
{
    public static string[] SupportedExtensions => MediaConverter.SupportedExtensions;

    public string OutputDirectory { get; private init; } = GetDefaultOutputDirectory();
    public string? FfmpegPath { get; private init; }
    public bool Recursive { get; private init; }
    public bool ShowHelp { get; private init; }
    public bool HasError { get; private init; }
    public IReadOnlyList<string> InputPaths { get; private init; } = Array.Empty<string>();

    public static CliOptions Parse(string[] args)
    {
        var inputPaths = new List<string>();
        var outputDirectory = GetDefaultOutputDirectory();
        string? ffmpegPath = null;
        var recursive = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            switch (arg)
            {
                case "-h":
                case "--help":
                    return new CliOptions { ShowHelp = true };
                case "-r":
                case "--recursive":
                    recursive = true;
                    break;
                case "-o":
                case "--output":
                    if (!TryReadValue(args, ref i, arg, out outputDirectory))
                        return Error();
                    break;
                case "--ffmpeg":
                    if (!TryReadValue(args, ref i, arg, out ffmpegPath))
                        return Error();
                    break;
                default:
                    if (arg.StartsWith("-", StringComparison.Ordinal))
                    {
                        Console.Error.WriteLine($"Unknown option: {arg}");
                        return Error();
                    }

                    inputPaths.Add(arg);
                    break;
            }
        }

        if (inputPaths.Count == 0)
        {
            Console.Error.WriteLine("At least one input file or directory is required.");
            return Error();
        }

        return new CliOptions
        {
            InputPaths = inputPaths,
            OutputDirectory = outputDirectory,
            FfmpegPath = ffmpegPath,
            Recursive = recursive
        };
    }

    private static bool TryReadValue(string[] args, ref int index, string optionName, out string value)
    {
        if (index + 1 >= args.Length || args[index + 1].StartsWith("-", StringComparison.Ordinal))
        {
            Console.Error.WriteLine($"{optionName} requires a value.");
            value = string.Empty;
            return false;
        }

        index++;
        value = args[index];
        return true;
    }

    private static CliOptions Error()
    {
        return new CliOptions { ShowHelp = true, HasError = true };
    }

    private static string GetDefaultOutputDirectory()
    {
        return Directory.Exists("/output")
            ? "/output"
            : Path.Combine(AppContext.BaseDirectory, "Output");
    }
}
