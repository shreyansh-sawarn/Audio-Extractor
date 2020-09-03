using System;
using System.Globalization;
using System.Windows.Data;

namespace AudioExtractor.Converters
{
    [ValueConversion(typeof(long), typeof(string))]
    class HumanReadableFileSizeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var fileSize = (long)value;
            return string.Format("{0:F2}k", (float)fileSize / 1024.0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
