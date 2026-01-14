using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SMTMS.Avalonia.Converters;

public class BoolToFontStyleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b && b)
        {
            return FontStyle.Italic;
        }
        return FontStyle.Normal;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
