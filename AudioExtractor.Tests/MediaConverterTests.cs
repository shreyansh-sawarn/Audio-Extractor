using AudioExtractor.Core;
using Xunit;

namespace AudioExtractor.Tests;

public class MediaConverterTests
{
    // ─── SupportedExtensions ─────────────────────────────────────────────────

    [Fact]
    public void SupportedExtensions_ContainsExpectedFormats()
    {
        var expected = new[] { "webm", "mp4", "mkv", "mov", "avi", "m4v" };
        Assert.Equal(expected, MediaConverter.SupportedExtensions);
    }

    [Theory]
    [InlineData("mp4")]
    [InlineData("mkv")]
    [InlineData("webm")]
    [InlineData("mov")]
    [InlineData("avi")]
    [InlineData("m4v")]
    public void SupportedExtensions_ContainsCommonVideoFormats(string ext)
    {
        Assert.Contains(ext, MediaConverter.SupportedExtensions, StringComparer.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("mp3")]
    [InlineData("wav")]
    [InlineData("txt")]
    [InlineData("exe")]
    public void SupportedExtensions_DoesNotContainAudioOrOtherFormats(string ext)
    {
        Assert.DoesNotContain(ext, MediaConverter.SupportedExtensions, StringComparer.OrdinalIgnoreCase);
    }

    // ─── Constructor validation ───────────────────────────────────────────────

    [Fact]
    public void Constructor_ThrowsArgumentException_WhenTargetDirectoryIsEmpty()
    {
        Assert.Throws<ArgumentException>(() => new MediaConverter(string.Empty));
    }

    [Fact]
    public void Constructor_ThrowsArgumentException_WhenTargetDirectoryIsWhitespace()
    {
        Assert.Throws<ArgumentException>(() => new MediaConverter("   "));
    }

    [Fact]
    public void Constructor_DoesNotThrow_WhenTargetDirectoryIsValid()
    {
        var ex = Record.Exception(() => new MediaConverter(Path.GetTempPath()));
        Assert.Null(ex);
    }

    // ─── IsFfmpegAvailable ────────────────────────────────────────────────────

    [Fact]
    public void IsFfmpegAvailable_ReturnsFalse_WhenPathDoesNotExist()
    {
        var converter = new MediaConverter(Path.GetTempPath(), "definitely_not_a_real_ffmpeg_path.exe");
        Assert.False(converter.IsFfmpegAvailable);
    }

    // ─── ConversionResult factory methods ────────────────────────────────────

    [Fact]
    public void ConversionResult_Failed_SetsSuccessfulToFalse()
    {
        var result = ConversionResult.Failed("input.mp4", "output.mp3", "Some error");
        Assert.False(result.Successful);
        Assert.Equal("input.mp4", result.InputPath);
        Assert.Equal("output.mp3", result.OutputPath);
        Assert.Equal("Some error", result.ErrorMessage);
    }

    [Fact]
    public void ConversionResult_Successful_IsTrue()
    {
        var result = new ConversionResult
        {
            InputPath = "input.mp4",
            OutputPath = "output.mp3",
            Successful = true,
            ExitCode = 0,
        };
        Assert.True(result.Successful);
        Assert.Equal(0, result.ExitCode);
    }

    // ─── ConvertFileAsync — missing file ─────────────────────────────────────

    [Fact]
    public async Task ConvertFileAsync_ReturnsFailure_WhenInputFileDoesNotExist()
    {
        var converter = new MediaConverter(Path.GetTempPath());
        var result = await converter.ConvertFileAsync("nonexistent_input_file_xyz.mp4");

        Assert.False(result.Successful);
        Assert.Contains("no longer exists", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    // ─── GetDuration — missing file ───────────────────────────────────────────

    [Fact]
    public void GetDuration_ReturnsZero_WhenInputFileDoesNotExist()
    {
        var converter = new MediaConverter(Path.GetTempPath());
        var duration = converter.GetDuration("nonexistent_file.mp4");
        Assert.Equal("00:00:00", duration);
    }
}
