using CasaEngine.Framework.Assets;
using CasaEngine.Framework.SceneManagement;

namespace CasaEngine.EditorUI.Controls.EntityControls.ViewModels;

public class EntityViewModel : NotifyPropertyChangeBase
{
    public AActor Entity { get; }
    public ComponentListViewModel ComponentListViewModel { get; }
    public ComponentsHierarchyViewModel ComponentsHierarchyViewModel { get; }

    public string Name
    {
        get => Entity.Name;
    }

    public EntityViewModel(AActor entity)
    {
        Entity = entity;
        ComponentListViewModel = new ComponentListViewModel(this);
        ComponentsHierarchyViewModel = new ComponentsHierarchyViewModel(this);

        AssetCatalog.AssetRenamed += OnAssetRenamed;
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
        ComponentsHierarchyViewModel.RootComponentViewModel.AddChildComponent(componentViewModel);
        Entity.AddComponent(componentViewModel.Component);
    }

    public void RemoveComponent(ComponentViewModel componentViewModel)
    {
        foreach (var child in ComponentsHierarchyViewModel.RootComponentViewModel.Children)
        {
            if (RemoveRemoveComponent(ComponentsHierarchyViewModel.RootComponentViewModel, child, componentViewModel))
            {
                return;
            }
        }
    }

    private bool RemoveRemoveComponent(ComponentViewModel parentComponentViewModel, ComponentViewModel componentViewModel, ComponentViewModel componentViewModelToRemove)
    {
        if (componentViewModel == componentViewModelToRemove)
        {
            parentComponentViewModel.RemoveComponent(componentViewModel);
            return true;
        }

        foreach (var child in componentViewModel.Children)
        {
            if (RemoveRemoveComponent(componentViewModel, child, componentViewModelToRemove))
            {
                return true;
            }
        }

        return false;
    }
}