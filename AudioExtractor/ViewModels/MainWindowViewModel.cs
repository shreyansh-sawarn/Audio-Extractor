using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Collections.ObjectModel;
using AudioExtractor.Models;
using Prism.Commands;
using System.Windows;
using System.Reflection;
using System.Windows.Forms;

namespace AudioExtractor.ViewModels
{
    class MainWindowViewModel : DependencyObject
    {
        public ObservableCollection<InputItemModel> InputItems { get; set; }
        public string[] FileTypeFilters { get; set; }

        public DelegateCommand StartConversionCommand { get; private set; }

        public DelegateCommand BrowseTargetFolderCommand { get; set; }

        public bool IsConversionInProgress
        {
            get { return (bool)GetValue(IsConversionInProgressProperty); }
            set { SetValue(IsConversionInProgressProperty, value); }
        }

        public static readonly DependencyProperty IsConversionInProgressProperty =
            DependencyProperty.Register("IsConversionInProgress", typeof(bool), typeof(MainWindowViewModel), new PropertyMetadata(false));

        public bool IsInputItemsEmpty
        {
            get { return (bool)GetValue(IsInputItemsEmptyProperty); }
            set { SetValue(IsInputItemsEmptyProperty, value); }
        }

        public static readonly DependencyProperty IsInputItemsEmptyProperty =
            DependencyProperty.Register("IsInputItemsEmpty", typeof(bool), typeof(MainWindowViewModel), new PropertyMetadata(true));

        public string TargetFolderPath
        {
            get { return (string)GetValue(TargetFolderPathProperty); }
            set { SetValue(TargetFolderPathProperty, value); }
        }

        public static readonly DependencyProperty TargetFolderPathProperty =
            DependencyProperty.Register("TargetFolderPath", typeof(string), typeof(MainWindowViewModel), new PropertyMetadata(default(string)));

        public MainWindowViewModel()
        {
            InputItems = new ObservableCollection<InputItemModel>();
            InputItems.CollectionChanged += (o, e) => { IsInputItemsEmpty = InputItems.Count == 0; };

            FileTypeFilters = new string[]
            {
                "webm",
                "mp4"
            };

            StartConversionCommand = new DelegateCommand(async () => await ConvertFiles(), () => !IsConversionInProgress);
            BrowseTargetFolderCommand = new DelegateCommand(() => BrowseTargetFolder(), () => !IsConversionInProgress);

            if (string.IsNullOrEmpty(Properties.Settings.Default.TargetFolderPath))
                Properties.Settings.Default.TargetFolderPath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "Output");

            TargetFolderPath = Properties.Settings.Default.TargetFolderPath;
        }

        public void AddFileList(string[] fileList)
        {
            if (fileList == null)
                return;

            foreach (var filePath in fileList)
            {
                var extension = Path.GetExtension(filePath).Remove(0, 1);

                if (string.IsNullOrEmpty(extension))
                    continue;

                if (!FileTypeFilters.Contains(extension, StringComparer.InvariantCultureIgnoreCase))
                    continue;

                InputItems.Add(new InputItemModel(filePath));
            }
        }

        private async Task ConvertFiles()
        {
            IsConversionInProgress = true;
            StartConversionCommand.RaiseCanExecuteChanged();
            BrowseTargetFolderCommand.RaiseCanExecuteChanged();

            if (!Directory.Exists(TargetFolderPath))
                Directory.CreateDirectory(TargetFolderPath);

            var converter = new MediaConverter(TargetFolderPath);
            await converter.ConvertFiles(InputItems.Select((item) => item.FilePath).ToArray());

            InputItems.Clear();

            IsConversionInProgress = false;
            StartConversionCommand.RaiseCanExecuteChanged();
            BrowseTargetFolderCommand.RaiseCanExecuteChanged();
        }

        private void BrowseTargetFolder()
        {
            var dialog = new FolderBrowserDialog()
            {
                RootFolder = Environment.SpecialFolder.Desktop,
                SelectedPath = TargetFolderPath,
                ShowNewFolderButton = true
            };

            if (dialog.ShowDialog() != DialogResult.OK)
                return;

            TargetFolderPath = dialog.SelectedPath;
            Properties.Settings.Default.TargetFolderPath = TargetFolderPath;
        }
    }
}
