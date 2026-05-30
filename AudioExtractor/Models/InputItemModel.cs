using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace AudioExtractor.Models;

public sealed class InputItemModel : INotifyPropertyChanged
{
    private string _status = "Queued";
    private string? _errorMessage;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string FileName { get; set; } = string.Empty;
    public string FileExtension { get; set; } = string.Empty;
    public string ContainerDirectory { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long FileSize { get; set; }

    private string? _outputPath;
    public string? OutputPath
    {
        get => _outputPath;
        set
        {
            if (SetProperty(ref _outputPath, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCompleted)));
            }
        }
    }

    public string Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCompleted)));
            }
        }
    }

    public bool IsCompleted => Status == "Done" && !string.IsNullOrEmpty(OutputPath);

    private double _progress;
    public double Progress
    {
        get => _progress;
        set => SetProperty(ref _progress, value);
    }

    private string _totalDuration = string.Empty;
    public string TotalDuration
    {
        get => _totalDuration;
        set
        {
            if (SetProperty(ref _totalDuration, value))
            {
                if (TryParseTime(value, out double seconds))
                {
                    _totalDurationSeconds = seconds;
                }
                ValidateTimeRange();
            }
        }
    }

    private double _totalDurationSeconds = -1;

    private bool _isTimeRangeValid = true;
    public bool IsTimeRangeValid
    {
        get => _isTimeRangeValid;
        private set => SetProperty(ref _isTimeRangeValid, value);
    }

    private string _timeValidationError = string.Empty;
    public string TimeValidationError
    {
        get => _timeValidationError;
        private set => SetProperty(ref _timeValidationError, value);
    }

    private string _startTime = string.Empty;
    public string StartTime
    {
        get => _startTime;
        set
        {
            if (SetProperty(ref _startTime, value))
            {
                ValidateTimeRange();
            }
        }
    }

    private string _endTime = string.Empty;
    public string EndTime
    {
        get => _endTime;
        set
        {
            if (SetProperty(ref _endTime, value))
            {
                ValidateTimeRange();
            }
        }
    }

    public void ValidateTimeRange()
    {
        if (string.IsNullOrWhiteSpace(StartTime))
        {
            IsTimeRangeValid = true;
            TimeValidationError = string.Empty;
            return;
        }

        if (!TryParseTime(StartTime, out double startSec))
        {
            IsTimeRangeValid = false;
            TimeValidationError = "Start time format invalid (use hh:mm:ss, mm:ss, or seconds).";
            return;
        }

        double endSec = startSec;
        if (!string.IsNullOrWhiteSpace(EndTime))
        {
            if (!TryParseTime(EndTime, out endSec))
            {
                IsTimeRangeValid = false;
                TimeValidationError = "End time format invalid (use hh:mm:ss, mm:ss, or seconds).";
                return;
            }
        }

        if (startSec > endSec)
        {
            IsTimeRangeValid = false;
            TimeValidationError = "Start time cannot be after End time.";
            return;
        }

        if (_totalDurationSeconds >= 0)
        {
            if (startSec > _totalDurationSeconds)
            {
                IsTimeRangeValid = false;
                TimeValidationError = $"Start time exceeds total media duration ({TotalDuration}).";
                return;
            }
            if (endSec > _totalDurationSeconds)
            {
                IsTimeRangeValid = false;
                TimeValidationError = $"End time exceeds total media duration ({TotalDuration}).";
                return;
            }
        }

        IsTimeRangeValid = true;
        TimeValidationError = string.Empty;
    }

    public static bool TryParseTime(string input, out double seconds)
    {
        seconds = 0;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        if (double.TryParse(input, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out seconds))
        {
            return seconds >= 0;
        }

        var parts = input.Split(':');
        if (parts.Length == 2)
        {
            if (int.TryParse(parts[0], out int min) && double.TryParse(parts[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double sec))
            {
                if (min >= 0 && sec >= 0 && sec < 60)
                {
                    seconds = min * 60 + sec;
                    return true;
                }
            }
        }
        else if (parts.Length == 3)
        {
            if (int.TryParse(parts[0], out int hr) && int.TryParse(parts[1], out int min) && double.TryParse(parts[2], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double sec))
            {
                if (hr >= 0 && min >= 0 && min < 60 && sec >= 0 && sec < 60)
                {
                    seconds = hr * 3600 + min * 60 + sec;
                    return true;
                }
            }
        }

        return false;
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public InputItemModel()
    {
    }

    public InputItemModel(string filePath)
    {
        var fileInfo = new FileInfo(filePath);

        FilePath = filePath;
        FileName = Path.GetFileNameWithoutExtension(fileInfo.Name);
        FileExtension = fileInfo.Extension.TrimStart('.').ToUpperInvariant();
        ContainerDirectory = fileInfo.DirectoryName ?? string.Empty;
        FileSize = fileInfo.Exists ? fileInfo.Length : 0;
    }

    private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(storage, value))
            return false;

        storage = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
