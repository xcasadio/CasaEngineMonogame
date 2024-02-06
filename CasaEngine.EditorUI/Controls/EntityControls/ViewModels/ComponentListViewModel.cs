using System.Collections.ObjectModel;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.SceneManagement.Components;

namespace CasaEngine.EditorUI.Controls.EntityControls.ViewModels;

public class ComponentListViewModel : NotifyPropertyChangeBase
{
    public ComponentViewModel RootComponentViewModel { get; private set; }

    public ObservableCollection<ComponentViewModel> ComponentsViewModel { get; } = new();

    public EntityViewModel EntityViewModel { get; }

    public ComponentListViewModel(EntityViewModel entityViewModel)
    {
        EntityViewModel = entityViewModel;

        RootComponentViewModel = new RootNodeComponentViewModel(EntityViewModel);
        ComponentsViewModel.Add(RootComponentViewModel);

        if (entityViewModel.Entity != null)
        {
            CreateComponentHierarchy(entityViewModel.Entity);
        }
    }

    private void CreateComponentHierarchy(Entity actor)
    {
        if (actor.RootComponent != null)
        {
            var componentViewModel = new ComponentViewModel(actor.RootComponent);
            componentViewModel.Parent = RootComponentViewModel;
            RootComponentViewModel.Children.Add(componentViewModel);

            foreach (var componentChild in actor.RootComponent.Children)
            {
                AddChild(componentViewModel, componentChild);
            }
        }

        foreach (var component in actor.Components)
        {
            var componentViewModel = new ComponentViewModel(component);
            componentViewModel.Parent = RootComponentViewModel;
            RootComponentViewModel.Children.Add(componentViewModel);

            if (component is SceneComponent sceneComponent)
            {
                foreach (var childComponent in sceneComponent.Children)
                {
                    AddChild(RootComponentViewModel, childComponent);
                }
            }
        }
    }

    private void AddChild(ComponentViewModel parentComponentViewModel, SceneComponent componentChild)
    {
        var childComponentViewModel = new ComponentViewModel(componentChild);
        childComponentViewModel.Parent = parentComponentViewModel;
        parentComponentViewModel.Children.Add(childComponentViewModel);

        foreach (var component in componentChild.Children)
        {
            AddChild(childComponentViewModel, component);
        }
    }

    public void RemoveComponent(EntityComponent component)
    {
        if (EntityViewModel.Entity.RootComponent == component)
        {
            EntityViewModel.Entity.RootComponent = null;
        }
        else
        {
            EntityViewModel.Entity.RemoveComponent(component);
        }
    }
}
