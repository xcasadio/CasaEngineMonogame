using CasaEngine.Framework.Assets;
using System.Collections.ObjectModel;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.SceneManagement.Components;
using static Assimp.Metadata;

namespace CasaEngine.EditorUI.Controls.EntityControls.ViewModels;

public class EntityViewModel : NotifyPropertyChangeBase
{
    private EntityViewModel? _parent;

    public virtual string Name
    {
        get => Entity.Name;
        set
        {
            if (value != Entity.Name && AssetCatalog.CanRename(value))
            {
                AssetCatalog.Rename(Entity.Id, value);
                Entity.Name = value;
                OnPropertyChanged();
            }
        }
    }

    public Entity Entity { get; }

    public EntityViewModel? Parent
    {
        get => _parent;
        internal set => SetField(ref _parent, value);
    }

    public ObservableCollection<EntityViewModel> Children { get; } = new();
    public ComponentListViewModel ComponentListViewModel { get; }

    public EntityViewModel(Entity entity)
    {
        Entity = entity;
        ComponentListViewModel = new ComponentListViewModel(this);

        AssetCatalog.AssetRenamed += OnAssetRenamed;

        if (entity?.Parent != null)
        {
            Parent = new EntityViewModel(entity.Parent);
        }
    }

    private void OnAssetRenamed(object? sender, Core.Design.EventArgs<AssetInfo, string> e)
    {
        if (e.Value.Id == Entity.Id)
        {
            OnPropertyChanged("Name");
        }
    }

    public void AddComponent(ComponentViewModel componentViewModel)
    {
        ComponentListViewModel.RootComponentViewModel.AddComponent(componentViewModel);
    }

    public virtual void AddActorChild(EntityViewModel entityViewModel)
    {
        entityViewModel.Parent = this;
        Entity.AddChild(entityViewModel.Entity);
        Children.Add(entityViewModel);
    }

    public virtual void RemoveActorChild(EntityViewModel entityViewModel)
    {
        entityViewModel.Parent = null;
        Entity.RemoveChild(entityViewModel.Entity);
        Children.Remove(entityViewModel);
    }

    public ComponentViewModel? GetFromComponent(SceneComponent sceneComponent)
    {
        var componentViewModel = GetFromEntity(ComponentListViewModel.RootComponentViewModel, sceneComponent);

        if (componentViewModel != null)
        {
            return componentViewModel;
        }

        foreach (var viewModel in ComponentListViewModel.ComponentsViewModel)
        {
            componentViewModel = GetFromEntity(viewModel, sceneComponent);

            if (componentViewModel != null)
            {
                return componentViewModel;
            }
        }

        return null;
    }

    private ComponentViewModel? GetFromEntity(ComponentViewModel componentViewModel, SceneComponent sceneComponent)
    {
        if (componentViewModel.Component == sceneComponent)
        {
            return componentViewModel;
        }

        foreach (var childEntityViewModel in componentViewModel.Children)
        {
            var found = GetFromEntity(childEntityViewModel, sceneComponent);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}