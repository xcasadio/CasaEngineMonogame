using System;
using System.Collections.Generic;
using System.Windows.Data;
using CasaEngine.Framework.SceneManagement;
using CasaEngine.Framework.Scripting;

namespace CasaEngine.EditorUI.Controls.EntityControls;

public class ExternalComponentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is IEnumerable<KeyValuePair<int, Type>> keyValuePairList)
        {
            return keyValuePairList;
        }

        if (value is KeyValuePair<Guid, Type> pair)
        {
            return pair;
        }

        if (value is IEnumerable<KeyValuePair<Guid, Type>> pairs)
        {
            return pairs;
        }

        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}