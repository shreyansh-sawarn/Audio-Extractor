using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using AudioExtractor.Core;
using AudioExtractor.Models;

namespace AudioExtractor.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private static readonly string[] SupportedExtensions = { "webm", "mp4", "mkv", "mov", "avi", "m4v" };
    private readonly AppSettings _settings;
    private bool _isConversionInProgress;
    private string _targetFolderPath;
    private string _statusMessage = "Drop video files to begin.";

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<InputItemModel> InputItems { get; } = new();

    public RelayCommand StartConversionCommand { get; }
    public RelayCommand BrowseTargetFolderCommand { get; }
    public RelayCommand ClearItemsCommand { get; }

    public bool IsConversionInProgress
    {
        get => _isConversionInProgress;
        private set
        {
            if (SetProperty(ref _isConversionInProgress, value))
                RaiseCommandStatesChanged();
        }
    }

    public bool IsInputItemsEmpty => InputItems.Count == 0;

    public string TargetFolderPath
    {
        get => _targetFolderPath;
        set
        {
            if (!SetProperty(ref _targetFolderPath, value))
                return;

            _settings.TargetFolderPath = value;
            _settings.Save();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public MainWindowViewModel()
    {
        _settings = AppSettings.Load();
        _targetFolderPath = _settings.TargetFolderPath;

        InputItems.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(IsInputItemsEmpty));
            RaiseCommandStatesChanged();
        };

        StartConversionCommand = new RelayCommand(async () => await ConvertFilesAsync(), CanStartConversion);
        BrowseTargetFolderCommand = new RelayCommand(BrowseTargetFolder, () => !IsConversionInProgress);
        ClearItemsCommand = new RelayCommand(ClearItems, () => !IsConversionInProgress && InputItems.Count > 0);
    }

    public void AddFileList(string[]? fileList)
    {
        if (fileList == null || IsConversionInProgress)
            return;

        var added = 0;
        var skipped = 0;

        foreach (var filePath in fileList.Where(File.Exists))
        {
            var extension = Path.GetExtension(filePath).TrimStart('.');

            if (string.IsNullOrWhiteSpace(extension) ||
                !SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                skipped++;
                continue;
            }

            if (InputItems.Any(item => string.Equals(item.FilePath, filePath, StringComparison.OrdinalIgnoreCase)))
            {
                skipped++;
                continue;
            }

            InputItems.Add(new InputItemModel(filePath));
            added++;
        }

        StatusMessage = added == 0
            ? "No supported new files were added."
            : skipped == 0
                ? $"Added {added} file(s)."
                : $"Added {added} file(s), skipped {skipped}.";
    }

    private bool CanStartConversion()
    {
        return !IsConversionInProgress && InputItems.Count > 0;
    }

    private async Task ConvertFilesAsync()
    {
        IsConversionInProgress = true;
        StatusMessage = "Converting...";

        try
        {
            Directory.CreateDirectory(TargetFolderPath);
            var converter = new MediaConverter(TargetFolderPath);

            var completed = 0;
            var failed = 0;

            foreach (var item in InputItems.ToArray())
            {
                item.Status = "Converting";
                item.ErrorMessage = null;

                var result = await converter.ConvertFileAsync(item.FilePath);
                completed++;

                if (result.Successful)
                {
                    item.OutputPath = result.OutputPath;
                    item.Status = "Done";
                }
                else
                {
                    failed++;
                    item.Status = "Failed";
                    item.ErrorMessage = result.ErrorMessage;
                }

                StatusMessage = $"Converted {completed} of {InputItems.Count}.";
            }

            StatusMessage = failed == 0
                ? $"Finished {completed} file(s)."
                : $"Finished with {failed} failed file(s).";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsConversionInProgress = false;
        }
    }

    private void BrowseTargetFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            RootFolder = Environment.SpecialFolder.Desktop,
            SelectedPath = TargetFolderPath,
            ShowNewFolderButton = true
        };

        if (dialog.ShowDialog() != DialogResult.OK)
            return;

        TargetFolderPath = dialog.SelectedPath;
    }

    private void ClearItems()
    {
        InputItems.Clear();
        StatusMessage = "Drop video files to begin.";
    }

    private void RaiseCommandStatesChanged()
    {
        StartConversionCommand.RaiseCanExecuteChanged();
        BrowseTargetFolderCommand.RaiseCanExecuteChanged();
        ClearItemsCommand.RaiseCanExecuteChanged();
    }

    private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(storage, value))
            return false;

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
