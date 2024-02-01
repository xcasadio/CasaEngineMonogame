using CasaEngine.Framework.Assets;
using CasaEngine.Framework.SceneManagement;
using System.Collections.ObjectModel;

namespace CasaEngine.EditorUI.Controls.EntityControls.ViewModels;

public class EntityViewModel : NotifyPropertyChangeBase
{
    private EntityViewModel? _parent;

    public string Name
    {
        get => Entity.Name;
    }

    public AActor Entity { get; }

    public EntityViewModel? Parent
    {
        get => _parent;
        internal set => SetField(ref _parent, value);
    }

    public ObservableCollection<EntityViewModel> Children { get; } = new();
    public ComponentsHierarchyViewModel ComponentsHierarchyViewModel { get; }

    public EntityViewModel(AActor entity)
    {
        Entity = entity;
        ComponentsHierarchyViewModel = new ComponentsHierarchyViewModel(this);

        AssetCatalog.AssetRenamed += OnAssetRenamed;

        if (entity.Parent != null)
        {
            Parent = new EntityViewModel(entity.Parent);
        }
    }

    private void OnAssetRenamed(object? sender, Core.Design.EventArgs<Framework.Assets.AssetInfo, string> e)
    {
        if (e.Value.Id == Entity.Id)
        {
            OnPropertyChanged("Name");
        }
    }

    public void AddComponent(ComponentViewModel componentViewModel)
    {
        ComponentsHierarchyViewModel.RootComponentViewModel.AddComponent(componentViewModel);
    }

    public void AddActorChild(EntityViewModel entityViewModel)
    {
        entityViewModel.Parent = this;
        Entity.AddChild(entityViewModel.Entity);
        Children.Add(entityViewModel);
    }

    public void RemoveActorChild(EntityViewModel entityViewModel)
    {
        entityViewModel.Parent = null;
        Entity.RemoveChild(entityViewModel.Entity);
        Children.Remove(entityViewModel);
    }
}