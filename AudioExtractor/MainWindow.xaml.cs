using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AudioExtractor.ViewModels;

namespace AudioExtractor;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel = new();
    private System.Windows.Point _dragStartPoint;
    private Models.InputItemModel? _draggedItem;
    private System.Windows.Forms.NotifyIcon? _notifyIcon;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        ApplyTheme(_viewModel.Theme);

        // Intercept drag/drop events globally even if listbox items consume/handle them
        AddHandler(DragDrop.DragEnterEvent, new System.Windows.DragEventHandler(Window_DragEnter), true);
        AddHandler(DragDrop.DragOverEvent, new System.Windows.DragEventHandler(Window_DragOver), true);

        // Initialize notification tray icon and hook up VM completion event
        InitializeTrayIcon();
        _viewModel.ConversionBatchCompleted += ViewModel_ConversionBatchCompleted;

        Logger.Log("Audio Extractor UI application started.");
        Task.Run(() => Logger.PurgeOldLogs());
    }

    private void InitializeTrayIcon()
    {
        try
        {
            _notifyIcon = new System.Windows.Forms.NotifyIcon
            {
                Icon = System.Drawing.SystemIcons.Information,
                Visible = true,
                Text = "Audio Extractor & Converter"
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to initialize Tray Icon: {ex.Message}");
            Logger.LogError("Failed to initialize Tray Icon", ex);
        }
    }

    private void ViewModel_ConversionBatchCompleted(int completedCount, int failedCount)
    {
        if (_notifyIcon == null) return;

        string title = "Conversion Complete";
        string message = failedCount == 0
            ? $"Successfully converted {completedCount} file(s)!"
            : $"Completed: {completedCount} succeeded, {failedCount} failed/cancelled.";

        _notifyIcon.ShowBalloonTip(3000, title, message, System.Windows.Forms.ToolTipIcon.Info);
        Logger.Log($"Tray notification shown: completed={completedCount}, failed={failedCount}");
    }

    protected override void OnClosed(EventArgs e)
    {
        Logger.Log("Audio Extractor UI application closing.");
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
        base.OnClosed(e);
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.Theme))
        {
            ApplyTheme(_viewModel.Theme);
        }
    }

    private void ApplyTheme(string themeName)
    {
        var app = System.Windows.Application.Current;
        app.Resources.MergedDictionaries.Clear();
        
        var themeUri = new Uri($"Themes/{themeName}Theme.xaml", UriKind.Relative);
        try
        {
            var dict = System.Windows.Application.LoadComponent(themeUri) as ResourceDictionary;
            if (dict != null)
            {
                app.Resources.MergedDictionaries.Add(dict);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load theme {themeName}: {ex.Message}");
        }
    }

    private void InputItemListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Avoid starting item drag if the user clicks inside input textboxes, buttons, etc.
        if (e.OriginalSource is DependencyObject depObj)
        {
            var parentTextBox = FindVisualParent<System.Windows.Controls.TextBox>(depObj);
            var parentButton = FindVisualParent<System.Windows.Controls.Button>(depObj);
            if (parentTextBox != null || parentButton != null)
            {
                _draggedItem = null;
                return;
            }
        }

        _dragStartPoint = e.GetPosition(null);
        _draggedItem = FindVisualParent<ListBoxItem>(e.OriginalSource as DependencyObject)?.Content as Models.InputItemModel;
    }

    private void InputItemListBox_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && _draggedItem != null)
        {
            var currentPosition = e.GetPosition(null);
            var diff = _dragStartPoint - currentPosition;

            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                var listBox = (System.Windows.Controls.ListBox)sender;
                var listBoxItem = listBox.ItemContainerGenerator.ContainerFromItem(_draggedItem) as ListBoxItem;
                if (listBoxItem != null)
                {
                    DragDrop.DoDragDrop(listBoxItem, _draggedItem, System.Windows.DragDropEffects.Move);
                    _draggedItem = null;
                }
            }
        }
    }

    private void InputItemListBox_Drop(object sender, System.Windows.DragEventArgs e)
    {
        // Handle files dropped from windows explorer
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            if (!_viewModel.IsConversionInProgress)
            {
                _viewModel.AddFileList(e.Data.GetData(System.Windows.DataFormats.FileDrop) as string[]);
            }
            return;
        }

        // Handle item reordering drop
        var draggedItem = e.Data.GetData(typeof(Models.InputItemModel)) as Models.InputItemModel;
        if (draggedItem == null) return;

        var listBox = (System.Windows.Controls.ListBox)sender;
        var dropTargetItem = FindVisualParent<ListBoxItem>(e.OriginalSource as DependencyObject)?.Content as Models.InputItemModel;

        if (dropTargetItem != null && draggedItem != dropTargetItem)
        {
            var items = _viewModel.InputItems;
            int oldIndex = items.IndexOf(draggedItem);
            int newIndex = items.IndexOf(dropTargetItem);

            if (oldIndex >= 0 && newIndex >= 0)
            {
                items.Move(oldIndex, newIndex);
            }
        }
        _draggedItem = null;
    }

    private void InputItemListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (InputItemListBox.SelectedItem is Models.InputItemModel item && item.IsCompleted)
        {
            _viewModel.PlayFileCommand.Execute(item);
        }
    }

    private void Window_DragEnter(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop) && !_viewModel.IsConversionInProgress)
        {
            DragDropOverlay.Visibility = Visibility.Visible;
            e.Effects = System.Windows.DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private void Window_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop) && !_viewModel.IsConversionInProgress)
        {
            e.Effects = System.Windows.DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private void DragDropOverlay_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop) && !_viewModel.IsConversionInProgress)
        {
            e.Effects = System.Windows.DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private void DragDropOverlay_DragLeave(object sender, System.Windows.DragEventArgs e)
    {
        var pos = e.GetPosition(this);
        if (pos.X < 0 || pos.Y < 0 || pos.X >= ActualWidth || pos.Y >= ActualHeight)
        {
            DragDropOverlay.Visibility = Visibility.Collapsed;
        }
        e.Handled = true;
    }

    private void DragDropOverlay_Drop(object sender, System.Windows.DragEventArgs e)
    {
        DragDropOverlay.Visibility = Visibility.Collapsed;
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop) && !_viewModel.IsConversionInProgress)
        {
            _viewModel.AddFileList(e.Data.GetData(System.Windows.DataFormats.FileDrop) as string[]);
        }
        e.Handled = true;
    }

    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child != null)
        {
            if (child is T parent)
                return parent;

            if (child is Visual || child is System.Windows.Media.Media3D.Visual3D)
            {
                child = VisualTreeHelper.GetParent(child);
            }
            else if (child is FrameworkContentElement fce)
            {
                child = fce.Parent;
            }
            else
            {
                child = null;
            }
        }
        return null;
    }
}
