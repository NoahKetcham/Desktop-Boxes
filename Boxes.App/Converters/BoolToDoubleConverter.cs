using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Boxes.App.Converters;

public class BoolToDoubleConverter : IValueConverter
{
    public double TrueValue { get; set; } = 1;
    public double FalseValue { get; set; } = 0;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? TrueValue : FalseValue;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double d)
        {
            return Math.Abs(d - TrueValue) < Math.Abs(d - FalseValue);
        }

        return false;
    }
}


