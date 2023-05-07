using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace CasaEngine.WpfControls;

public abstract class ValueConverterBase<T> : MarkupExtension, IValueConverter where T : class, IValueConverter, new()
{
    private static T _valueConverterInstance;

    protected ValueConverterBase()
    {
        if (GetType() != typeof(T))
        {
            throw new InvalidOperationException("The generic argument of this class must be the type being implemented.");
        }
    }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return _valueConverterInstance ??= new T();
    }

    public abstract object Convert(object value, Type targetType, object parameter, CultureInfo culture);

    public abstract object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture);
}