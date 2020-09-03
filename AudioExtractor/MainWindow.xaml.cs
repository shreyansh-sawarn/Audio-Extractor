using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using AudioExtractor.ViewModels;

namespace AudioExtractor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainWindowViewModel m_viewModel = new MainWindowViewModel();

        public MainWindow()
        {
            InitializeComponent();

            DataContext = m_viewModel;

            // To make the UI design process easier we use test data in the XAML,
            // which must be cleared before filling up the list box with real data
            InputItemListBox.Items.Clear();
            InputItemListBox.ItemsSource = m_viewModel.InputItems;

            Closing += (o, e) => { Properties.Settings.Default.Save(); };
        }

        private void InputItemListBox_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent("FileDrop"))
                return;

            var fileList = e.Data.GetData("FileDrop");
            m_viewModel.AddFileList(fileList as string[]);
        }

        
    }
}
