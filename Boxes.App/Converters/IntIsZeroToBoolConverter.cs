using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Boxes.App.Converters;

public sealed class IntIsZeroToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int i)
        {
            return i == 0;
        }

        if (value is string s && int.TryParse(s, out var parsed))
        {
            return parsed == 0;
        }

        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}


