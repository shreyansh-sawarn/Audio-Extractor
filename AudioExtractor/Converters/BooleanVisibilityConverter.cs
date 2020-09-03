using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AudioExtractor.Converters
{
    [ValueConversion(typeof(bool), typeof(Visibility))]
    class BooleanVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (targetType != typeof(Visibility))
                throw new InvalidOperationException("Target type must be Visibility");

            return (bool)value ? Visibility.Visible : Visibility.Hidden;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
