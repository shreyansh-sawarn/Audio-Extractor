using System.Windows;
using AudioExtractor.ViewModels;

namespace AudioExtractor;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        ApplyTheme(_viewModel.Theme);
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

    private void InputItemListBox_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            return;

        _viewModel.AddFileList(e.Data.GetData(System.Windows.DataFormats.FileDrop) as string[]);
    }

    private void InputItemListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
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
        DragDropOverlay.Visibility = Visibility.Collapsed;
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
}
