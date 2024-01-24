using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.SceneManagement;

namespace CasaEngine.EditorUI.Controls.EntityControls;

public class ExternalComponentRegisteredDataAccess
{
    ObservableCollection<KeyValuePair<Guid, Type>> ExternalComponentsById = new();

    public ObservableCollection<KeyValuePair<Guid, Type>> GetDatas()
    {
        foreach (var keyValuePair in GameSettings.ElementFactory.GetDerivedTypesFrom<GameplayProxy>())
        {
            ExternalComponentsById.Add(keyValuePair);
        }

        return ExternalComponentsById;
    }

}