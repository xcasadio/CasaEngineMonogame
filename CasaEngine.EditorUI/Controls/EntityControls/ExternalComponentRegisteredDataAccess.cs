using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CasaEngine.Framework.Game;

namespace CasaEngine.Editor.Controls.EntityControls;

public class ExternalComponentRegisteredDataAccess
{
    ObservableCollection<KeyValuePair<int, Type>> ExternalComponentsById = new();

    public ObservableCollection<KeyValuePair<int, Type>> GetDatas()
    {
        foreach (var keyValuePair in GameSettings.ScriptLoader.TypesById)
        {
            ExternalComponentsById.Add(keyValuePair);
        }

        return ExternalComponentsById;
    }

}