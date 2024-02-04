using System;
using System.Collections.Generic;
using System.Windows.Data;

namespace CasaEngine.EditorUI.Controls.EntityControls;

public class ScriptClassNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is IEnumerable<KeyValuePair<Guid, Type>> pairs)
        {
            return pairs;
        }

        if (value is KeyValuePair<Guid, Type> pair)
        {
            return pair;
        }

        if (value is string name)
        {
            return name;
        }

        if (value is IEnumerable<string> types)
        {
            return types;
        }

        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}