using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace predskoz;

public static class BoolToBrushConverter
{
    public static readonly IValueConverter Instance = new FuncValueConverter<bool, IBrush>(value =>
        value ? new SolidColorBrush(Color.Parse("#4a6a4a")) : new SolidColorBrush(Color.Parse("#6a4a4a")));
}

public static class BoolToIconConverter
{
    public static readonly IValueConverter Instance = new FuncValueConverter<bool, string>(value =>
        value ? "✅" : "⚠️");
}