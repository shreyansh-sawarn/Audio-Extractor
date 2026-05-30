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
}
