using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace predskoz.Converters;

public class StringToPointsConverter : IValueConverter
{
    public static readonly StringToPointsConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string str || string.IsNullOrWhiteSpace(str))
            return new Points();

        var points = new Points();
        var pairs = str.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        foreach (var pair in pairs)
        {
            var coords = pair.Split(',');
            if (coords.Length == 2 &&
                double.TryParse(coords[0], out var x) &&
                double.TryParse(coords[1], out var y))
            {
                points.Add(new Point(x, y));
            }
        }

        return points;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}