using System;
using System.Collections.Generic;
using System.Windows.Data;
using CasaEngine.Framework.Scripting;

namespace CasaEngine.Editor.Controls.EntityControls;

public class ExternalComponentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is IEnumerable<KeyValuePair<int, Type>> keyValuePairList)
        {
            return keyValuePairList;
        }

        if (value is ExternalComponent externalComponent)
        {
            return new KeyValuePair<int, Type>(externalComponent.ExternalComponentId, externalComponent.GetType());
        }

        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}