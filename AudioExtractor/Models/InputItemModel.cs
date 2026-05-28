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
    public string? OutputPath { get; set; }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
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

    private void SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(storage, value))
            return;

        storage = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
