using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Game;

namespace CasaEngine.Editor.Controls.EntityControls;

public class EntityViewModel : NotifyPropertyChangeBase
{
    public Entity Entity { get; }

    public string Name
    {
        get => Entity.Name;
    }

    public EntityViewModel(Entity entity)
    {
        Entity = entity;

        GameSettings.AssetInfoManager.AssetRenamed += OnAssetRenamed;
    }

    private void OnAssetRenamed(object? sender, Core.Design.EventArgs<Framework.Assets.AssetInfo, string> e)
    {
        if (e.Value.Id == Entity.AssetInfo.Id)
        {
            OnPropertyChanged("Name");
        }
    }
}