using System.Collections.ObjectModel;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Scripting;

namespace CasaEngine.EditorUI.Controls.EntityControls;

public class GameplayProxyClassesRegisteredDataAccess
{
    ObservableCollection<string> ExternalComponentsById = new();

    public ObservableCollection<string> GetDatas()
    {
        foreach (var type in ElementFactory.GetDerivedTypesFrom<GameplayProxy>())
        {
            ExternalComponentsById.Add(type.Name);
        }

        return ExternalComponentsById;
    }

}