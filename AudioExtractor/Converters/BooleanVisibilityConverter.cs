using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AudioExtractor.Converters;

[ValueConversion(typeof(bool), typeof(Visibility))]
public sealed class BooleanVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not bool visible)
            return Visibility.Collapsed;

        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
