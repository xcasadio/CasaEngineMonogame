using System;
using System.Runtime.CompilerServices;
using System.Windows;

namespace CasaEngine.WpfControls;

public static class ConverterHelper
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ConvertToBoolean(object value, IFormatProvider culture)
    {
        return value != DependencyProperty.UnsetValue && Convert.ToBoolean(value, culture);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static char ConvertToChar(object value, IFormatProvider culture)
    {
        return value != DependencyProperty.UnsetValue ? Convert.ToChar(Convert.ToUInt32(value), culture) : default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double ConvertToDouble(object value, IFormatProvider culture)
    {
        return value != DependencyProperty.UnsetValue ? Convert.ToDouble(value, culture) : default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ConvertToString(object value, IFormatProvider culture)
    {
        return value != DependencyProperty.UnsetValue ? Convert.ToString(value, culture) : string.Empty;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object ChangeType(object value, Type targetType, IFormatProvider culture)
    {
        // Retrieve the underlying type if the target type is a nullable.
        return value != DependencyProperty.UnsetValue ? Convert.ChangeType(value, targetType, culture) : targetType.Default();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool? TryConvertToBoolean(object value, IFormatProvider culture)
    {
        return value != null && value != DependencyProperty.UnsetValue ? ConvertToBoolean(value, culture) : null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static char? TryConvertToChar(object value, IFormatProvider culture)
    {
        return value != null && value != DependencyProperty.UnsetValue ? ConvertToChar(value, culture) : null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double? TryConvertToDouble(object value, IFormatProvider culture)
    {
        return value != null && value != DependencyProperty.UnsetValue ? ConvertToDouble(value, culture) : null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string TryConvertToString(object value, IFormatProvider culture)
    {
        return value != null && value != DependencyProperty.UnsetValue ? ConvertToString(value, culture) : null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object TryChangeType(object value, Type targetType, IFormatProvider culture)
    {
        // Retrieve the underlying type if the target type is a nullable.
        targetType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        return value != null && value != DependencyProperty.UnsetValue ? Convert.ChangeType(value, targetType, culture) : null;
    }
}