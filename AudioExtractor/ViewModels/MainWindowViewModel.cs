using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using AudioExtractor.Core;
using AudioExtractor.Infrastructure;
using AudioExtractor.Models;

namespace AudioExtractor.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly AppSettings _settings;
    private bool _isConversionInProgress;
    private string _targetFolderPath;
    private string _statusMessage = "Drop video files to begin.";
    private CancellationTokenSource? _cts;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<int, int>? ConversionBatchCompleted;

    public ObservableCollection<InputItemModel> InputItems { get; } = new();

    public string[] SupportedFormats { get; } = { "MP3", "M4A", "WAV", "FLAC" };
    public string[] SupportedBitrates { get; } = { "128k", "256k", "320k" };
    public string[] SupportedPostActions { get; } = { "None", "Play Sound", "Open Folder" };
    public int[] SupportedParallelOptions { get; } = { 1, 2, 3, 4 };

    private string _selectedFormat = "MP3";
    public string SelectedFormat
    {
        get => _selectedFormat;
        set => SetProperty(ref _selectedFormat, value);
    }

    private string _selectedBitrate = "256k";
    public string SelectedBitrate
    {
        get => _selectedBitrate;
        set
        {
            if (SetProperty(ref _selectedBitrate, value))
            {
                _settings.Bitrate = value;
                _settings.Save();
            }
        }
    }

    private bool _isMono;
    public bool IsMono
    {
        get => _isMono;
        set
        {
            if (SetProperty(ref _isMono, value))
            {
                _settings.IsMono = value;
                _settings.Save();
            }
        }
    }

    private bool _normalize;
    public bool Normalize
    {
        get => _normalize;
        set
        {
            if (SetProperty(ref _normalize, value))
            {
                _settings.Normalize = value;
                _settings.Save();
            }
        }
    }

    private string _selectedPostAction = "None";
    public string SelectedPostAction
    {
        get => _selectedPostAction;
        set
        {
            if (SetProperty(ref _selectedPostAction, value))
            {
                _settings.PostAction = value;
                _settings.Save();
            }
        }
    }

    private string _theme = "Dark";
    public string Theme
    {
        get => _theme;
        set
        {
            if (SetProperty(ref _theme, value))
            {
                _settings.Theme = value;
                _settings.Save();
                OnPropertyChanged(nameof(IsDarkMode));
            }
        }
    }

    public bool IsDarkMode
    {
        get => Theme == "Dark";
        set => Theme = value ? "Dark" : "Light";
    }

    private int _maxParallelTasks = 1;
    public int MaxParallelTasks
    {
        get => _maxParallelTasks;
        set
        {
            if (SetProperty(ref _maxParallelTasks, value))
            {
                _settings.MaxParallelTasks = value;
                _settings.Save();
            }
        }
    }

    private string _filenameTemplate = "[Name]";
    public string FilenameTemplate
    {
        get => _filenameTemplate;
        set
        {
            if (SetProperty(ref _filenameTemplate, value))
            {
                _settings.FilenameTemplate = value;
                _settings.Save();
            }
        }
    }

    public RelayCommand StartConversionCommand { get; }
    public RelayCommand CancelConversionCommand { get; }
    public RelayCommand BrowseTargetFolderCommand { get; }
    public RelayCommand ClearItemsCommand { get; }
    public RelayCommand ClearCompletedCommand { get; }
    public RelayCommand AddFilesCommand { get; }
    public RelayCommand<InputItemModel> PlayFileCommand { get; }
    public RelayCommand<InputItemModel> OpenFolderCommand { get; }
    public RelayCommand<InputItemModel> RemoveItemCommand { get; }

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
            Logger.LogDirectory = Path.Combine(value, "logs");
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
        _targetFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "AudioExtractor");
        _settings.TargetFolderPath = _targetFolderPath;
        Logger.LogDirectory = Path.Combine(_targetFolderPath, "logs");

        // Restore user settings
        _selectedBitrate = _settings.Bitrate;
        _isMono = _settings.IsMono;
        _normalize = _settings.Normalize;
        _selectedPostAction = _settings.PostAction;
        _theme = _settings.Theme;
        _maxParallelTasks = _settings.MaxParallelTasks > 0 ? _settings.MaxParallelTasks : 1;
        _filenameTemplate = !string.IsNullOrEmpty(_settings.FilenameTemplate) ? _settings.FilenameTemplate : "[Name]";

        _settings.Save();

        InputItems.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(IsInputItemsEmpty));
            RaiseCommandStatesChanged();
        };

        StartConversionCommand = new RelayCommand(async () => await ConvertFilesAsync(), CanStartConversion);
        CancelConversionCommand = new RelayCommand(CancelConversion, () => IsConversionInProgress);
        BrowseTargetFolderCommand = new RelayCommand(BrowseTargetFolder, () => !IsConversionInProgress);
        ClearItemsCommand = new RelayCommand(ClearItems, () => !IsConversionInProgress && InputItems.Count > 0);
        ClearCompletedCommand = new RelayCommand(ClearCompleted, () => !IsConversionInProgress && InputItems.Any(item => item.IsCompleted));
        AddFilesCommand = new RelayCommand(AddFiles, () => !IsConversionInProgress);
        PlayFileCommand = new RelayCommand<InputItemModel>(PlayFile, item => item?.IsCompleted == true);
        OpenFolderCommand = new RelayCommand<InputItemModel>(OpenFolder, item => item?.IsCompleted == true);
        RemoveItemCommand = new RelayCommand<InputItemModel>(RemoveItem, item => !IsConversionInProgress);
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
                !MediaConverter.SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                skipped++;
                continue;
            }

            if (InputItems.Any(item => string.Equals(item.FilePath, filePath, StringComparison.OrdinalIgnoreCase)))
            {
                skipped++;
                continue;
            }

            var item = new InputItemModel(filePath)
            {
                StartTime = "00:00:00",
                EndTime = "00:00:00"
            };
            InputItems.Add(item);
            added++;

            var targetDir = TargetFolderPath;
            Task.Run(() =>
            {
                try
                {
                    var converter = new MediaConverter(targetDir);
                    var duration = converter.GetDuration(filePath);
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        item.TotalDuration = duration;
                        item.EndTime = duration;
                    });
                }
                catch
                {
                }
            });
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

    private string ClassifyError(string rawError, string inputPath)
    {
        if (string.IsNullOrWhiteSpace(rawError))
            return "Unknown error occurred.";

        var lowerError = rawError.ToLowerInvariant();

        if (lowerError.Contains("no longer exists"))
            return "The input file was moved, renamed, or deleted before conversion could start.";

        if (lowerError.Contains("ffmpeg was not found"))
            return "FFmpeg executable is missing. Please check if ffmpeg is placed inside the '3rdparty' folder.";

        if (lowerError.Contains("permission denied") || lowerError.Contains("access denied") || lowerError.Contains("access is denied"))
            return "Access denied to target directory. Try choosing another output folder or running the app as administrator.";

        if (lowerError.Contains("no space left") || lowerError.Contains("disk full"))
            return "Your disk is full. Free up space and try again.";

        if (lowerError.Contains("invalid data found") || lowerError.Contains("moov atom not found") || lowerError.Contains("invalid argument"))
            return "Corrupted or unsupported input file. Ensure this file plays correctly in a media player.";

        if (lowerError.Contains("invalid channel layout") || lowerError.Contains("loudnorm"))
            return "FFmpeg parameter error. Try disabling 'Force Mono' or 'Normalize Volume' settings.";

        if (lowerError.Contains("operation cancelled") || lowerError.Contains("cancelled"))
            return "Conversion was cancelled by the user.";

        return $"{rawError} (Check target folder and input file path)";
    }

    private async Task ConvertFilesAsync()
    {
        IsConversionInProgress = true;
        StatusMessage = "Converting...";
        _cts = new CancellationTokenSource();

        Logger.Log($"Batch conversion started. Files in queue: {InputItems.Count}. Settings: Format={SelectedFormat}, Bitrate={SelectedBitrate}, Mono={IsMono}, Normalize={Normalize}, MaxParallel={MaxParallelTasks}, Template={FilenameTemplate}, TargetFolder={TargetFolderPath}");

        try
        {
            Directory.CreateDirectory(TargetFolderPath);
            var converter = new MediaConverter(TargetFolderPath);

            var completed = 0;
            var failed = 0;
            var lockObj = new object();

            using var semaphore = new SemaphoreSlim(MaxParallelTasks);

            var tasks = InputItems.ToArray().Select(async item =>
            {
                try
                {
                    await semaphore.WaitAsync(_cts.Token);
                }
                catch (OperationCanceledException)
                {
                    item.Status = "Cancelled";
                    lock (lockObj) failed++;
                    Logger.Log($"Conversion cancelled before starting for: {item.FilePath}");
                    return;
                }

                try
                {
                    if (_cts.IsCancellationRequested)
                    {
                        item.Status = "Cancelled";
                        lock (lockObj) failed++;
                        Logger.Log($"Conversion cancelled before starting for: {item.FilePath}");
                        return;
                    }

                    item.Status = "Converting";
                    item.ErrorMessage = null;
                    item.Progress = 0;

                    Logger.Log($"Starting conversion for: {item.FilePath} | StartTime={item.StartTime}, EndTime={item.EndTime}");

                    var progressReporter = new Progress<double>(p => item.Progress = p);
                    var result = await converter.ConvertFileAsync(
                        item.FilePath,
                        SelectedFormat,
                        SelectedBitrate,
                        IsMono,
                        Normalize,
                        item.StartTime,
                        item.EndTime,
                        FilenameTemplate,
                        progressReporter,
                        _cts.Token);

                    if (_cts.IsCancellationRequested)
                    {
                        item.Status = "Cancelled";
                        lock (lockObj) failed++;
                        Logger.Log($"Conversion cancelled for: {item.FilePath}");
                    }
                    else if (result.Successful)
                    {
                        item.OutputPath = result.OutputPath;
                        item.Status = "Done";
                        lock (lockObj) completed++;
                        Logger.Log($"Successfully converted: {item.FilePath} -> {result.OutputPath}");
                    }
                    else
                    {
                        item.Status = "Failed";
                        item.ErrorMessage = ClassifyError(result.ErrorMessage ?? string.Empty, item.FilePath);
                        lock (lockObj) failed++;
                        Logger.LogError($"Failed converting: {item.FilePath} | Raw Error: {result.ErrorMessage} | Classified: {item.ErrorMessage}");
                    }
                }
                catch (OperationCanceledException)
                {
                    item.Status = "Cancelled";
                    lock (lockObj) failed++;
                    Logger.Log($"Conversion cancelled for: {item.FilePath}");
                }
                catch (Exception ex)
                {
                    item.Status = "Failed";
                    item.ErrorMessage = ClassifyError(ex.Message, item.FilePath);
                    lock (lockObj) failed++;
                    Logger.LogError($"Exception during conversion of {item.FilePath}", ex);
                }
                finally
                {
                    semaphore.Release();

                    int currentCompleted, currentTotal;
                    lock (lockObj)
                    {
                        currentCompleted = completed;
                        currentTotal = InputItems.Count;
                    }
                    StatusMessage = $"Processed {currentCompleted} of {currentTotal}.";
                }
            });

            await Task.WhenAll(tasks);

            if (_cts.IsCancellationRequested)
            {
                StatusMessage = "Conversion cancelled.";
                Logger.Log($"Batch conversion cancelled by user. Completed: {completed}, Failed/Cancelled: {failed}");
            }
            else
            {
                StatusMessage = failed == 0
                    ? $"Finished {completed} file(s)."
                    : $"Finished with {failed} failed/cancelled file(s).";

                Logger.Log($"Batch conversion completed. Successful: {completed}, Failed/Cancelled: {failed}");

                RunPostConversionAction();
                ConversionBatchCompleted?.Invoke(completed, failed);
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Conversion cancelled.";
            Logger.Log("Batch conversion task cancelled.");
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            Logger.LogError("Exception in Batch conversion loop", ex);
        }
        finally
        {
            if (_cts != null)
            {
                _cts.Dispose();
                _cts = null;
            }
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

    private void AddFiles()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Multiselect = true,
            Filter = "Video Files (*.webm;*.mp4;*.mkv;*.mov;*.avi;*.m4v)|*.webm;*.mp4;*.mkv;*.mov;*.avi;*.m4v|All Files (*.*)|*.*",
            Title = "Select Video Files to Convert"
        };

        if (dialog.ShowDialog() == true)
        {
            AddFileList(dialog.FileNames);
        }
    }

    private void PlayFile(InputItemModel item)
    {
        if (item == null || string.IsNullOrEmpty(item.OutputPath) || !File.Exists(item.OutputPath))
            return;

        try
        {
            Process.Start(new ProcessStartInfo(item.OutputPath)
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to play file: {ex.Message}";
        }
    }

    private void OpenFolder(InputItemModel item)
    {
        if (item == null || string.IsNullOrEmpty(item.OutputPath) || !File.Exists(item.OutputPath))
            return;

        try
        {
            Process.Start("explorer.exe", $"/select,\"{item.OutputPath}\"");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to open folder: {ex.Message}";
        }
    }

    private void RemoveItem(InputItemModel item)
    {
        if (item != null)
        {
            InputItems.Remove(item);
        }
    }

    private void CancelConversion()
    {
        _cts?.Cancel();
        StatusMessage = "Stopping...";
    }

    private void ClearCompleted()
    {
        var completedItems = InputItems.Where(item => item.IsCompleted).ToList();
        foreach (var item in completedItems)
        {
            InputItems.Remove(item);
        }
        StatusMessage = $"Cleared {completedItems.Count} completed file(s).";
    }

    private void RunPostConversionAction()
    {
        switch (SelectedPostAction)
        {
            case "Play Sound":
                System.Media.SystemSounds.Exclamation.Play();
                break;
            case "Open Folder":
                try
                {
                    Process.Start("explorer.exe", TargetFolderPath);
                }
                catch
                {
                }
                break;
            case "None":
            default:
                break;
        }
    }

    private void RaiseCommandStatesChanged()
    {
        StartConversionCommand.RaiseCanExecuteChanged();
        CancelConversionCommand.RaiseCanExecuteChanged();
        BrowseTargetFolderCommand.RaiseCanExecuteChanged();
        ClearItemsCommand.RaiseCanExecuteChanged();
        ClearCompletedCommand.RaiseCanExecuteChanged();
        AddFilesCommand.RaiseCanExecuteChanged();
        RemoveItemCommand.RaiseCanExecuteChanged();
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
