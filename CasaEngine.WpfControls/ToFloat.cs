using System;
using System.Globalization;

namespace CasaEngine.WpfControls;

public class ToFloat : ValueConverterBase<ToFloat>
{
    public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return targetType == typeof(double) ? ConverterHelper.ConvertToDouble(value, culture) : ConverterHelper.TryConvertToDouble(value, culture);
    }

    public override object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return targetType.IsValueType && !targetType.IsNullable() ? ConverterHelper.ChangeType(value, targetType, culture) : ConverterHelper.TryChangeType(value, targetType, culture);
    }
}