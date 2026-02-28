using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace Boxes.App.Converters;

public class BoolToGridLengthConverter : IValueConverter
{
    public double TrueValue { get; set; } = 340;
    public double FalseValue { get; set; } = 0;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var length = value is true ? TrueValue : FalseValue;
        return new GridLength(length);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is GridLength gl && gl.IsAbsolute)
        {
            return Math.Abs(gl.Value - TrueValue) < Math.Abs(gl.Value - FalseValue);
        }
        return false;
    }
}
