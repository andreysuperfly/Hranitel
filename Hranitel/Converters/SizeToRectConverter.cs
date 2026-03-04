using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Hranitel.Converters;

/// <summary> Конвертер для Clip с скруглёнными углами: MultiBinding (Width, Height) -> Rect. </summary>
public class SizeToRectConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var w = values.Length > 0 && values[0] is double dw && !double.IsNaN(dw) ? dw : 0;
        var h = values.Length > 1 && values[1] is double dh && !double.IsNaN(dh) ? dh : 0;
        if (w <= 0) w = 0;
        if (h <= 0) h = 1;
        return new Rect(0, 0, w, h);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
