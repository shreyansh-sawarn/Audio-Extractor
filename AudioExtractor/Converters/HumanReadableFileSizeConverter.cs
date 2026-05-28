using System;
using System.Globalization;
using System.Windows.Data;

namespace AudioExtractor.Converters;

[ValueConversion(typeof(long), typeof(string))]
public sealed class HumanReadableFileSizeConverter : IValueConverter
{
    private static readonly string[] Units = { "B", "KB", "MB", "GB", "TB" };

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not long bytes)
            return string.Empty;

        var size = (double)bytes;
        var unit = 0;

        while (size >= 1024 && unit < Units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return string.Format(culture, "{0:0.##} {1}", size, Units[unit]);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
