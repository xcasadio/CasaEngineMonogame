using CasaEngine.Framework.Game;
using CasaEngine.Framework.SceneManagement;

namespace CasaEngine.EditorUI.Controls.EntityControls;

public class EntityViewModel : NotifyPropertyChangeBase
{
    public AActor Entity { get; }
    public ComponentListViewModel ComponentListViewModel { get; }

    public string Name
    {
        get => Entity.Name;
    }

    public EntityViewModel(AActor entity)
    {
        Entity = entity;
        ComponentListViewModel = new ComponentListViewModel(this);

        GameSettings.AssetCatalog.AssetRenamed += OnAssetRenamed;
    }

    private void OnAssetRenamed(object? sender, Core.Design.EventArgs<Framework.Assets.AssetInfo, string> e)
    {
        if (e.Value.Id == Entity.Id)
        {
            OnPropertyChanged("Name");
        }
    }
}