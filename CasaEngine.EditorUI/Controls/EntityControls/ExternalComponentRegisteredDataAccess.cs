using System;
using System.Collections.ObjectModel;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.SceneManagement;

namespace CasaEngine.EditorUI.Controls.EntityControls;

public class ExternalComponentRegisteredDataAccess
{
    ObservableCollection<Type> ExternalComponentsById = new();

    public ObservableCollection<Type> GetDatas()
    {
        foreach (var type in ElementFactory.GetDerivedTypesFrom<GameplayProxy>())
        {
            ExternalComponentsById.Add(type);
        }

        return ExternalComponentsById;
    }

}