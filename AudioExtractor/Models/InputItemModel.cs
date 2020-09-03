using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace AudioExtractor.Models
{
    class InputItemModel : DependencyObject
    {
        public string FileName { get; set; }
        public string FileExtension { get; set; }
        public string ContainerDirectory { get; set; }
        public string FilePath { get; set; }
        public long FileSize { get; set; }

        public bool Converted
        {
            get { return (bool)GetValue(ConvertedProperty); }
            set { SetValue(ConvertedProperty, value); }
        }

        public static readonly DependencyProperty ConvertedProperty =
            DependencyProperty.Register("Converted", typeof(bool), typeof(InputItemModel), new PropertyMetadata(false));

        public InputItemModel()
        {
        }

        public InputItemModel(string filePath)
        {
            var fileInfo = new FileInfo(filePath);

            FilePath = filePath;
            FileName = Path.GetFileNameWithoutExtension(fileInfo.Name);
            FileExtension = fileInfo.Extension.Remove(0, 1).ToUpper();
            ContainerDirectory = fileInfo.Directory.FullName;
            FileSize = fileInfo.Length;
        }
    }
}
