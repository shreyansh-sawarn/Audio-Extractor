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

    private string _startTime = string.Empty;
    public string StartTime
    {
        get => _startTime;
        set => SetProperty(ref _startTime, value);
    }

    private string _endTime = string.Empty;
    public string EndTime
    {
        get => _endTime;
        set => SetProperty(ref _endTime, value);
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
