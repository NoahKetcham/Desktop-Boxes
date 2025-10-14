using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Boxes.App.Converters;

public class BooleanToBrushConverter : IValueConverter
{
    public IBrush? TrueBrush { get; set; }
    public IBrush? FalseBrush { get; set; }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool flag)
        {
            return flag ? TrueBrush : FalseBrush;
        }

        return FalseBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (targetType == typeof(bool) && value is IBrush brush)
        {
            return Equals(brush, TrueBrush);
        }

        return null;
    }
}

